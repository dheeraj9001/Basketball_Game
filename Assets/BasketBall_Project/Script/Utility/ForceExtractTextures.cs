using UnityEditor;
using UnityEngine;

public class ForceExtractTextures
{
    [MenuItem("Assets/Force Extract Textures from FBX")]
    static void Extract()
    {
        foreach (var obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;

            string folder = EditorUtility.SaveFolderPanel("Extract textures to", "Assets", "");
            if (string.IsNullOrEmpty(folder)) continue;

            // Make path relative to project
            if (folder.StartsWith(Application.dataPath))
                folder = "Assets" + folder.Substring(Application.dataPath.Length);

            bool success = importer.ExtractTextures(folder);
            Debug.Log($"{path} → ExtractTextures returned: {success}");
            AssetDatabase.Refresh();
        }
    }
}