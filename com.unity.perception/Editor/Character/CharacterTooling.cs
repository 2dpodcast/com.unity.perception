using System.Collections.Generic;
using System.Linq;
using UnityEngine.Perception.GroundTruth;

namespace UnityEngine.Perception.Content
{
    public class CharacterTooling : MonoBehaviour
    {
        /// <summary>
        /// Bool function used for testing to make sure the target character has the required 15 starting bones
        /// </summary>
        /// <param name="selection">target character selected</param>
        /// <param name="failed">Dictionary return if of Human Bones that tracks they are prsent or missing</param>
        /// <returns>True if all 15 bones are present, otherwise false</returns>
        public bool CharacterRequiredBones(GameObject selection, out Dictionary<HumanBone, bool> failed)
        {
            var result = CharacterValidation.AvatarRequiredBones(selection);
            failed = new Dictionary<HumanBone, bool>();

            for (int i = 0; i < result.Count; i++)
            {
                var bone = result.ElementAt(i);
                var boneKey = bone.Key;
                var boneValue = bone.Value;

                if (boneValue != true)
                    failed.Add(boneKey, boneValue);
            }

            return failed.Count == 0;
        }

        /// <summary>
        /// Ensures there is pose data in the parent and child game objects of a character by checking for position and rotation
        /// </summary>
        /// <param name="gameObject">Target character selected</param>
        /// <param name="failedGameObjects">List of game objects that don't have nay pose data</param>
        /// <returns>The count of failed bones</returns>
        public bool CharacterPoseData(GameObject gameObject, out List<GameObject> failedGameObjects)
        {
            failedGameObjects = new List<GameObject>();

            var componentsParent = gameObject.GetComponents<Transform>();
            var componentsChild = gameObject.GetComponentsInChildren<Transform>();

            for (int p = 0; p < componentsParent.Length; p++)
            {
                var pos = componentsParent[p].transform.position;
                var rot = componentsParent[p].transform.rotation.eulerAngles;

                if (pos == null || rot == null)
                {
                    failedGameObjects.Add(componentsParent[p].gameObject);
                }
            }

            for (int c = 0; c < componentsChild.Length; c++)
            {
                var pos = componentsChild[c].transform.position;
                var rot = componentsChild[c].transform.rotation.eulerAngles;

                if (pos == null || rot == null)
                {
                    failedGameObjects.Add(componentsChild[c].gameObject);
                }
            }

            return failedGameObjects.Count == 0;
        }

        /// <summary>
        /// Bool function to make create a new prefab Character with nose and ear joints
        /// </summary>
        /// <param name="selection"></param>
        /// <param name="drawRays"></param>
        /// <param name="savePath"></param>
        /// <returns>True if the model is created, false and a new game object named Failed if the model wasn't created</returns>
        public bool CharacterCreateNose(GameObject selection, out GameObject newModel,Object keypointTemplate, bool drawRays = false, string savePath = "Assets/")
        {
            newModel = CharacterValidation.AvatarCreateNoseEars(selection, keypointTemplate, savePath, drawRays);

            if (newModel.name.Contains("Failed"))
            {
                GameObject.DestroyImmediate(newModel);
                return false;
            }
            else return true;
        }

        /// <summary>
        /// Validates the model to ensure the nose, ear L & R are created 
        /// </summary>
        /// <param name="selection">Game Object selected by tge yser</param>
        /// <returns>True if the nose, ear L & R was created, false if the joints are missing</returns>
        public bool ValidateNoseAndEars(GameObject selection)
        {
            var jointLabels = selection.GetComponentsInChildren<JointLabel>();
            var nose = false;
            var earRight = false;
            var earLeft = false;

            for (int i = 0; i < jointLabels.Length; i++)
            {
                if (jointLabels[i].name.Contains("nose"))
                    nose = true;
                if (jointLabels[i].name.Contains("earRight"))
                    earRight = true;
                if (jointLabels[i].name.Contains("earLeft"))
                    earLeft = true;
            }

            return nose && earRight && earLeft;
        }
    }
}