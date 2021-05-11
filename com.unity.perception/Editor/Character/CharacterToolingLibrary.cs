using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Perception.GroundTruth;
using UnityEngine.Windows;

namespace UnityEngine.Perception.Content
{
    public static class CharacterValidation
    {
        public static string[] s_RequiredBones =
        {
            "Head",
            "Hips",
            "Spine",
            "LeftUpperArm",
            "LeftLowerArm",
            "LeftHand",
            "RightUpperArm",
            "RightLowerArm",
            "RightHand",
            "LeftUpperLeg",
            "LeftLowerLeg",
            "LeftFoot",
            "RightUpperLeg",
            "RightLowerLeg",
            "RightFoot",
        };

        /// <summary>
        /// Checks the selected character fbx or prefab to make sure the required bones defined bu the strong [] RequiredBones
        /// </summary>
        /// <param name="selection">GameObject selected by the user in the editor</param>
        /// <returns>Dictionary of the Human Bone and bool to track presence</returns>
        public static Dictionary<HumanBone, bool> AvatarRequiredBones(GameObject selection)
        {
            var result = new Dictionary<HumanBone, bool>();
            var selectionInstance = (GameObject)PrefabUtility.InstantiatePrefab(selection);

            if (selectionInstance != null)
                selection = selectionInstance;

            var animator = selection.GetComponentInChildren<Animator>();
            var bone = new HumanBone();

            if (animator == null)
            {
                Debug.LogWarning("Animator and/or the Skinned Mesh Renderer are missing or can't be found!");
                result.Add(bone, false);
                GameObject.DestroyImmediate(selection);
                return result;
            }

            var human = animator.avatar.humanDescription.human;
            var totalBones = 0;

            for(int h = 0; h < human.Length; h++)
            {
                for (int b = 0; b < s_RequiredBones.Length; b++)
                {
                    if(human[h].humanName == s_RequiredBones[b])
                    {
                        if (human[h].boneName != null)
                        {
                            bone.boneName = human[h].boneName;
                            totalBones = totalBones = +1;
                            result.Add(bone, true);
                        }
                        else
                        {
                            result.Add(bone, false);
                        }
                    }
                }
            }
            GameObject.DestroyImmediate(selection);
            return result;
        }

        /// <summary>
        /// Based on the selection of a fbx or prefab character will find the location of the nose, left ear, and right ear joints
        /// based on eye and the center head locations. 
        /// </summary>
        /// <param name="selection"> fbx or prefab selection</param>
        /// <param name="savePath">Path where the new created prefab will be saved too</param>
        /// <param name="drawRays">Shows the rays on how the joint posiitons are found</param>
        /// <returns></returns>
        public static GameObject AvatarCreateNoseEars (GameObject selection, Object keypointTemplate, string savePath, bool drawRays = false)
        {
            if (selection == null)
            {
                Debug.LogWarning("Selected Game Object is null or missing");
                return new GameObject("Failed");
            }

            var selectionInstance = (GameObject)PrefabUtility.InstantiatePrefab(selection);
            if (selectionInstance != null)
                selection = selectionInstance;
            var animator = selection.GetComponentInChildren<Animator>();
            var skinnedMeshRenderer = selection.GetComponentInChildren<SkinnedMeshRenderer>();

            if (animator == null || skinnedMeshRenderer == null)
            {
                Debug.LogWarning("Animator and/or the Skinned Mesh Renderer are missing or can't be found!");
                return new GameObject("Failed");
            }

            var human = animator.avatar.humanDescription.human;
            var skeleton = animator.avatar.humanDescription.skeleton;
            var verticies = skinnedMeshRenderer.sharedMesh.vertices;

            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            //Currently stage left and stage right for the eyes
            var leftEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            var rightEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);

            var faceCenter = Vector3.zero;
            var earCenter = Vector3.zero;
            var nosePos = Vector3.zero;
            var earRightPos = Vector3.zero;
            var earLeftPos = Vector3.zero;
            var distanceCheck = 1f;

            var eyeDistance = 0f;
            var directionLeft = Vector3.zero;
            var directionRight = Vector3.zero;

            var rayHead = new Ray();
            var rayLeftEye = new Ray();
            var rayRightEye = new Ray();
            var noseRayFor = new Ray();
            var rayNoseBack = new Ray();
            var rayEarLeft = new Ray();
            var rayEarRight = new Ray();

            if(leftEye == null || rightEye == null)
            {
                Debug.LogWarning("Eye positions are null, unable to position nose joint!");
                return new GameObject("Failed");
            }
            else
            {
                // getting the angle direction from the start point of the eyes and the distance between the left and right eyes
                eyeDistance = Vector3.Distance(leftEye.position, rightEye.position);
                directionLeft = Quaternion.AngleAxis(-45, -leftEye.right) * -leftEye.up;
                directionRight = Quaternion.AngleAxis(-45, rightEye.right) * -rightEye.up;
            }

            rayLeftEye.origin = leftEye.position;
            rayLeftEye.direction = directionLeft * eyeDistance;

            rayRightEye.origin = rightEye.position;
            rayRightEye.direction = directionRight * eyeDistance;

            // Find the center of the face by taking where the left and right eye rays drawn at 45 degrees intersect to find
            // the average point of the nose
            for (var i = 0f; i < distanceCheck; i += 0.01f)
            {
                var pointR = rayRightEye.GetPoint(i);
                var pointL = rayLeftEye.GetPoint(i);

                var distanceX = Math.Abs(pointR.x - pointL.x);
                var distanceY = Math.Abs(pointR.y - pointL.y);

                if (distanceX < 0.01 && distanceY < 0.01 )
                {
                    faceCenter = pointR;
                }
            }
            
            rayHead.origin = head.position;
            rayHead.direction = Vector3.up * distanceCheck;
                        
            noseRayFor.origin = faceCenter;
            noseRayFor.direction = Vector3.forward * distanceCheck;
            
            rayNoseBack.origin = faceCenter;
            rayNoseBack.direction = Vector3.back * distanceCheck;

            // Find the ear center which can be used to go left and right to find the edge of the mesh for the ear
            for (var i = 0f; i < distanceCheck; i += 0.01f)
            {
                var pointH = rayHead.GetPoint(i);
                var pointF = rayNoseBack.GetPoint(i);

                var distanceZ = Math.Abs(pointH.z - pointF.z);

                if (distanceZ < 0.01f)
                {
                    earCenter = pointF;
                }
            }

            var allPos = new Dictionary<Vector3, float>();
            // Find the position of the nose using the nose ray from the starting point found by the eyes
            for (int v = 0; v < verticies.Length; v++)
            {
                for (var c = eyeDistance / 2; c < distanceCheck; c += 0.001f)
                {
                    var pointNoseRay = noseRayFor.GetPoint(c);
                    var pointVert = verticies[v];
                    var def = 0.005f;
                    var offset = pointVert - pointNoseRay;
                    var len = offset.sqrMagnitude;
                    

                    if(len < def * def)
                    {
                        nosePos = pointNoseRay;
                        if(!allPos.ContainsKey(pointNoseRay))
                            allPos.Add(pointNoseRay, len);
                    }
                }
            }
                        
            rayEarRight.origin = earCenter;
            rayEarRight.direction = Vector3.right * distanceCheck;
            
            rayEarLeft.origin = earCenter;
            rayEarLeft.direction = Vector3.left * distanceCheck;

            // Find both the left and right ear from the ear center in the right and left directions 
            for (int v = 0; v < verticies.Length; v++)
            {
                for (var c = eyeDistance / 2; c < distanceCheck; c += 0.001f)
                {
                    var pointEarRight = rayEarRight.GetPoint(c);
                    var pointEarLeft = rayEarLeft.GetPoint(c);
                    var pointVert = verticies[v];
                    var def = 0.09f;
                    var offsetR = pointVert - pointEarRight;
                    // TODO: Need to fix the left offset because of negative numbers
                    var offsetL = pointVert - pointEarLeft;
                    var lenR = offsetR.sqrMagnitude;
                    var lenL = offsetL.sqrMagnitude;

                    if (lenR < def * def || lenR < def)
                    {
                        earRightPos = pointEarRight;
                    }

                    if (lenL < def * def || lenL < def)
                    {
                        earLeftPos = pointEarLeft;
                    }
                }
            }

            var earLeftCheck = Vector3.Distance(earLeftPos, earCenter);
            var earRightCheck = Vector3.Distance(earRightPos, earCenter);
            var eyeCenterDistance = eyeDistance + 0.01f;

            if (earLeftCheck > eyeCenterDistance)
            {
                earLeftPos.x = (-eyeDistance);
            }

            if (earRightCheck > eyeCenterDistance)
            {
                earRightPos.x = eyeDistance;
            }

            if(drawRays)
            {
                DebugDrawRays(30f, distanceCheck, rightEye, leftEye, head, rayRightEye, rayLeftEye, faceCenter, earCenter);
            }

            return CreateNewCharacterPrefab(selection, nosePos, earRightPos, earLeftPos, keypointTemplate, savePath);
        }

        /// <summary>
        /// Drays the rays used to create the nose and ears 
        /// </summary>
        /// <param name="duration">Duration of the ray being drawn</param>
        /// <param name="distanceCheck">How far the ray goes</param>
        /// <param name="rightEye">transform of the right eye</param>
        /// <param name="leftEye">transform of the left eye</param>
        /// <param name="head">transform of the head</param>
        /// <param name="rayRightEye">ray from the right eye transform</param>
        /// <param name="rayLeftEye">ray from the left eye transform</param>
        /// <param name="faceCenter">center of face found from the eyes</param>
        /// <param name="earCenter">center of eyes found from the center of the head</param>
        public static void DebugDrawRays(float duration, float distanceCheck, Transform rightEye, Transform leftEye, Transform head,Ray rayRightEye, Ray rayLeftEye, Vector3 faceCenter, Vector3 earCenter)
        {
            Debug.DrawLine(rightEye.position, leftEye.position, Color.magenta, duration);
            Debug.DrawRay(rayLeftEye.origin, rayLeftEye.direction, Color.green, duration);
            Debug.DrawRay(rayRightEye.origin, rayRightEye.direction, Color.blue, duration);
            Debug.DrawRay(faceCenter, Vector3.forward * distanceCheck, Color.cyan, duration);
            Debug.DrawRay(faceCenter, Vector3.back, Color.cyan, duration);
            Debug.DrawRay(head.position, Vector3.up, Color.red, duration);
            Debug.DrawRay(earCenter, Vector3.right, Color.blue, duration);
            Debug.DrawRay(earCenter, Vector3.left, Color.green, duration);
        }

        /// <summary>
        /// Grabs the positions for the nose and ears then creates the joint, adds labels, parents the joints to the head, then saves the prefab
        /// </summary>
        /// <param name="selection">target character</param>
        /// <param name="nosePosition">Vector 3 position</param>
        /// <param name="earRightPosition">Vector 3 position</param>
        /// <param name="earLeftPosition">Vector 3 position</param>
        /// <param name="savePath">Save path for the creation of a new prefab</param>
        /// <returns></returns>
        public static GameObject CreateNewCharacterPrefab(GameObject selection, Vector3 nosePosition, Vector3 earRightPosition, Vector3 earLeftPosition, Object keypointTemplate, string savePath = "Assets/")
        {
            var head = FindBodyPart(selection, HumanBodyBones.Head);

            if (savePath == string.Empty || !Directory.Exists(savePath))
            {
                savePath = "Assets/";
                Debug.Log("Save file path didn't exist, defaulting to Assets folder in project!");
            }

            var filePath = savePath + selection.name + ".prefab";

            if (head != null)
            {
                if (nosePosition != Vector3.zero)
                {
                    var nose = new GameObject();
                    nose.transform.position = nosePosition;
                    nose.name = "nose";
                    nose.transform.SetParent(head);

                    AddJointLabel(nose, keypointTemplate);
                }

                if (earRightPosition != Vector3.zero)
                {
                    var earRight = new GameObject();
                    earRight.transform.position = earRightPosition;
                    earRight.name = "earRight";
                    earRight.transform.SetParent(head);

                    AddJointLabel(earRight, keypointTemplate);
                }

                if (earLeftPosition != Vector3.zero)
                {
                    var earLeft = new GameObject();
                    earLeft.transform.position = earLeftPosition;
                    earLeft.name = "earLeft";
                    earLeft.transform.SetParent(head);

                    AddJointLabel(earLeft, keypointTemplate);
                }
            }

            var model = PrefabUtility.SaveAsPrefabAsset(selection, filePath);

            return model;
        }

        /// <summary>
        /// Finds that name of the bone Transform that matches the target string 
        /// </summary>
        /// <param name="name">Target name of the bone that we need to find</param>
        /// <param name="root">Target selection transform</param>
        /// <returns>Will return the target Human bone as a Transform, or returns null</returns>
        public static Transform FindBodyPart(string name, Transform root)
        {
            var children = new List<Transform>();
            root.GetComponentsInChildren(children);

            foreach (var child in children)
            {
                if (child.name == name || child.name.Contains(name))
                    return child;
            }

            return null;
        }

        /// <summary>
        /// Finds that Human Bone in the Avatar of the animator of the character
        /// </summary>
        /// <param name="selection">Target Character that we need to find a part on</param>
        /// <param name="humanBone">Target Human Body Bone</param>
        /// <returns>Will return the target Human bone as a Transform, or returns null</returns>
        public static Transform FindBodyPart(GameObject selection, HumanBodyBones humanBone)
        {
            var animator = selection.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                var targetBone = animator.GetBoneTransform(humanBone);

                if (targetBone != null)
                    return targetBone;
            }

            return null;
        }

        /// <summary>
        /// Will ensure the target game object's avatar has the target Human Bone
        /// </summary>
        /// <param name="selection">Target game object</param>
        /// <param name="humanBone">Target human bone we are sreaching for in the avatar of the selected game object</param>
        /// <returns>Return true if the bone was found, or false if the bone is missing</returns>
        public static bool ValidateBodyPart(GameObject selection, HumanBodyBones humanBone)
        {
            var animator = selection.GetComponentInChildren<Animator>();

            if (animator != null)
            {
                var targetBone = animator.GetBoneTransform(humanBone);

                if (targetBone != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Add a joint label and add the template data for the joint, uses base CocoKeypointTemplate in perception since the plan is to
        /// remove the template from the template data
        /// </summary>
        /// <param name="gameObject">target gameobject from the joint</param>
        /// <param name="keypointTemplate">selected keypoint template from the UI, if blank it will grab the example CocoKeypointTemplate</param>
        static void AddJointLabel(GameObject gameObject, Object keypointTemplate)
        {
            var jointLabel = gameObject.AddComponent<JointLabel>();
            var data = new JointLabel.TemplateData();
            var exampleTemplate = AssetDatabase.GetAllAssetPaths().Where(o => o.EndsWith("CocoKeypointTemplate.asset", StringComparison.OrdinalIgnoreCase)).ToList();

            if (keypointTemplate == null)
            {
                foreach (string o in exampleTemplate)
                {
                    if (o.Contains("com.unity.perception"))
                    {
                        keypointTemplate = AssetDatabase.LoadAssetAtPath<Object>(o);
                    }
                }
            }

            data.label = gameObject.name;
            data.template = (KeypointTemplate)keypointTemplate;
            jointLabel.templateInformation = new List<JointLabel.TemplateData>();
            jointLabel.templateInformation.Add(data);
        }
    }
}