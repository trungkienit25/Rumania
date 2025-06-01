using UnityEngine;
using UnityEditor;
using Unity.Netcode;
using UnityEditor.SceneManagement;

namespace NetcodePlus.EditorTool
{
    /// <summary>
    /// Fix network ID duplicate issue
    /// </summary>

    public class GenerateNetworkID : ScriptableWizard
    {
        [MenuItem("Netcode/Generate Scene Network IDs", priority = 500)]
        static void ScriptableWizardMenu()
        {
            ScriptableWizard.DisplayWizard<GenerateNetworkID>("Generate Scene Network IDs", "Generate");
        }

        void OnWizardCreate()
        {
            SNetworkObject.editor_auto_gen_id = true; //Reset to true in case was off

            NetworkObject[] nobjs = FindObjectsOfType<NetworkObject>();
            foreach (NetworkObject nobj in nobjs)
            {
                EditorUtility.SetDirty(nobj);
            }

            SNetworkObject[] sobjs = FindObjectsOfType<SNetworkObject>();
            foreach (SNetworkObject sobj in sobjs)
            {
                sobj.GenerateEditorID();
                EditorUtility.SetDirty(sobj);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        void OnWizardUpdate()
        {
            helpString = "Use this tool when you receive error that there are duplicate NetworkObject ids.";
        }
    }
}