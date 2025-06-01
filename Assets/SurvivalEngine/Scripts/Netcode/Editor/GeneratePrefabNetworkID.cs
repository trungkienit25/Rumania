using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using UnityEditor.SceneManagement;

namespace NetcodePlus.EditorTool
{
    /// <summary>
    /// Fix network ID duplicate issue
    /// </summary>

    public class GeneratePrefabNetworkID : ScriptableWizard
    {
        [MenuItem("Netcode/Generate Prefab Network IDs", priority = 500)]
        static void ScriptableWizardMenu()
        {
            ScriptableWizard.DisplayWizard<GeneratePrefabNetworkID>("Generate Prefab Network IDs", "Generate");
        }

        void OnWizardCreate()
        {
            foreach (object obj in Selection.objects)
            {
                if (obj is GameObject)
                {
                    GameObject gobj = (GameObject)obj;
                    NetworkObject nobj = gobj.GetComponent<NetworkObject>();
                    if(nobj != null)
                        EditorUtility.SetDirty(nobj);

                    SNetworkObject sobj = gobj.GetComponent<SNetworkObject>();
                    if (sobj != null)
                    {
                        sobj.GenerateEditorID();
                        EditorUtility.SetDirty(sobj);
                    }
                }
            }

            AssetDatabase.SaveAssets();
        }

        void OnWizardUpdate()
        {
            helpString = "Select the prefabs you want to fix before using this.";
        }
    }
}