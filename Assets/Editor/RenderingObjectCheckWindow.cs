using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

using System.Text;
using System.Collections.Generic;

[UnityEditor.InitializeOnLoad]
public class RenderingObjectCheckWindow : EditorWindow
{

    private struct MaterialBind
    {
        public GameObject gameObject;
        public Material material;
        public Renderer renderer;
        public Graphic graphic;

        public MaterialBind(GameObject gmo, Renderer r,Material mat)
        {
            this.gameObject = gmo;
            this.material = mat;
            this.renderer = r;
            this.graphic = null;
        }
        public MaterialBind(GameObject gmo,Graphic g, Material mat )
        {
            this.gameObject = gmo;
            this.material = mat;
            this.renderer = null;
            this.graphic = g;
        }
    }
    private List<Shader> unityBuiltInShaderList = new List<Shader>();


    private List<Shader> projectShaderList = new List<Shader>();
    private List<MaterialBind> drawedObjectList = new List<MaterialBind>();
    private Vector2 scrollPos;

    private int selectTab = 0;
    private static readonly string[] selectTabTitles = { "ProjectのShader","UnityのBuiltIn Shader", "描画一覧" };

    private int drawedObjectCondition;
    private static readonly string[] drawedObjectConditionTitles = { "全て", "ProjectにあるShader利用","Unityの組み込みShader利用","AssetBundleのShader利用" };

    [MenuItem("Tools/RenderingObjectCheck")]
    public static void Create()
    {
        EditorWindow.GetWindow<RenderingObjectCheckWindow>();
    }


    private void GetBuiltInShaders(){
        // UnityBuiltInのシェーダー一覧
        unityBuiltInShaderList.Clear();

        var defaultResource = AssetDatabase.LoadAllAssetsAtPath("Library/unity default resources");
        foreach (var res in defaultResource)
        {
            Shader shader = res as Shader;
            if (shader == null) { continue; }
            unityBuiltInShaderList.Add(shader);
        }

        var builtInExtra = AssetDatabase.LoadAllAssetsAtPath("Resources/unity_builtin_extra");
        foreach (var res in builtInExtra)
        {
            Shader shader = res as Shader;
            if (shader == null) { continue; }
            unityBuiltInShaderList.Add(shader);
        }
    }

    void OnEnable()
    {
        GetBuiltInShaders();

        GetProjectShaderList();
        FindAllDrawedObjectList();
    }


    private void GetProjectShaderList()
    {
        // projectのシェーダー
        projectShaderList.Clear();
        var guids = AssetDatabase.FindAssets("t:Shader");
        if (guids == null) { return; }
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) { continue; }
            projectShaderList.Add(shader);

            // built-inリストに誤認されてしまっているようなら消します
            if (IsInstanceIdExists(shader.GetInstanceID(), unityBuiltInShaderList))
            {
                unityBuiltInShaderList.Remove(shader);
            }
        }
    }


    private void FindAllDrawedObjectList()
    {
        drawedObjectList.Clear();
        var allRenderer = Object.FindObjectsOfType<Renderer>();
        var allUiGraphic = Object.FindObjectsOfType<Graphic>();

        foreach (var r in allRenderer)
        {
            var materials = r.sharedMaterials;
            if (materials == null) { continue; }
            foreach (var m in materials)
            {
                if (m == null) { continue; }
                this.drawedObjectList.Add(new MaterialBind(r.gameObject, r, m));
            }
        }
        foreach (var gr in allUiGraphic)
        {
            var material = gr.materialForRendering;
            if (material == null) { continue; }
            this.drawedObjectList.Add(new MaterialBind(gr.gameObject,gr, material));
        }
    }
    private void Reload()
    {
        GetProjectShaderList();
        FindAllDrawedObjectList();
    }

    void OnGUI()
    {
        if (GUILayout.Button("更新",GUILayout.Width(40) ))
        {
            Reload();
        }
        selectTab = GUILayout.Toolbar(selectTab, selectTabTitles);

        
        switch (selectTab)
        {
            case 0:
                OnGUIShaders("Project内のShader一覧 ", projectShaderList);
                break;
            case 1:
                OnGUIShaders("Unityの組み込みShader一覧", unityBuiltInShaderList);
                break;
            case 2:
                OnGUIDrawObjects();
                break;
        }

    }
    private void OnGUIShaders(string title,List<Shader> shaderList){
        EditorGUILayout.LabelField(title);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        List<Shader> removeList = new List<Shader>();
        foreach (var shader in shaderList)
        {
            if (!shader)
            {
                removeList.Add(shader);
                continue;
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(shader, typeof(Shader), GUILayout.Width(200.0f) );
            EditorGUILayout.LabelField("InstanceID:" + shader.GetInstanceID() + " hide:" + shader.hideFlags);
            EditorGUILayout.EndHorizontal();
        }
        foreach (var remove in removeList)
        {
            shaderList.Remove(remove);
        }
        EditorGUILayout.EndScrollView();
    }
    private void OnGUIDrawObjects()
    {
        EditorGUILayout.LabelField("現在描画中のモノ一覧");
        drawedObjectCondition = EditorGUILayout.Popup("表示条件",drawedObjectCondition, drawedObjectConditionTitles);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        foreach (var m in drawedObjectList)
        {
            // materialが破棄された？
            if (!m.material)
            {
                continue;
            }
            // gameObjectが破棄された？
            if (!m.gameObject)
            {
                continue;
            }
            // shader nullはキッと何かがおかしい
            if (m.material.shader == null)
            {
                continue;
            }
            // 表示条件チェック
            bool outputFlag = true;
            switch (drawedObjectCondition)
            {

                // "ProjectにあるShader利用",
                case 1:
                    outputFlag = IsInstanceIdExists(m.material.shader.GetInstanceID(), projectShaderList);
                    break;
                //"Unityの組み込みShader利用",
                case 2:
                    outputFlag = IsInstanceIdExists(m.material.shader.GetInstanceID(), unityBuiltInShaderList);
                    break;
                // "実行して読み込まれたShader利用"
                case 3:
                    outputFlag = ( (!IsInstanceIdExists(m.material.shader.GetInstanceID(), projectShaderList) ) &&
                            (!IsInstanceIdExists(m.material.shader.GetInstanceID(), unityBuiltInShaderList) ) );
                    break;
            }
            if (!outputFlag)
            {
                continue;
            }

            StringBuilder sb = new StringBuilder(32);


            sb.Append("使用Shader:");
            if (m.material.shader.name != null)
            {
                sb.Append(m.material.shader.name);
            }


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(m.gameObject, typeof(GameObject), GUILayout.Width(200.0f));
            EditorGUILayout.ObjectField(m.material, typeof(Material), GUILayout.Width(200.0f));
            EditorGUILayout.LabelField(sb.ToString());
            EditorGUILayout.LabelField("ShaderInstanceID:" + m.material.shader.GetInstanceID());

            EditorGUILayout.EndHorizontal();

        }
        EditorGUILayout.EndScrollView();
    }

    // インスタンスIDがあるかチェックします
    private bool IsInstanceIdExists(int instanceId, List<Shader> shaders)
    {
        foreach (var shader in shaders)
        {
            if (shader != null && shader.GetInstanceID() == instanceId)
            {
                return true;
            }
        }
        return false;
    }
}
