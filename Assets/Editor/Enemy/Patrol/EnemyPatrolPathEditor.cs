using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(EnemyPatrolPath))]
public class EnemyPatrolPathEditor : Editor
{
    ReorderableList _list;
    SerializedProperty _waypoints;
    SerializedProperty _moveSpeed;
    SerializedProperty _turnSpeed;
    SerializedProperty _arrive;
    SerializedProperty _loop;

    void OnEnable()
    {
        _waypoints = serializedObject.FindProperty("waypoints");
        _moveSpeed = serializedObject.FindProperty("moveSpeed");
        _turnSpeed = serializedObject.FindProperty("turnSpeedDegrees");
        _arrive = serializedObject.FindProperty("arriveDistance");
        _loop = serializedObject.FindProperty("loop");

        _list = new ReorderableList(serializedObject, _waypoints, true, true, true, true);
        _list.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "巡逻路径点（相对本锚点物体本地坐标）");
        };
        _list.elementHeight = EditorGUIUtility.singleLineHeight * 2f + 6f;
        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            var el = _waypoints.GetArrayElementAtIndex(index);
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, el.FindPropertyRelative("localPosition"),
                new GUIContent($"点 {index} 本地坐标"));
            rect.y += EditorGUIUtility.singleLineHeight + 2f;
            EditorGUI.PropertyField(rect, el.FindPropertyRelative("waitSeconds"), new GUIContent("到达后停留(秒)"));
        };
    }

    public override void OnInspectorGUI()
    {
        var path = (EnemyPatrolPath)target;

        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "路径点为相对「本物体」Transform 的本地坐标，适用于任意敌人生成点或巡逻锚点。选中本物体可在 Scene 中查看折线。\n" +
            "运行时由关卡/生成逻辑把该配置应用到敌人身上的 EnemyPatrolAgent（例如 TestLevelEnemyBootstrap）。敌人上若有实现 IEnemyPatrolSuspendCondition 的组件，会在交火/警戒时暂停巡逻。",
            MessageType.Info);

        _list.DoLayoutList();

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("添加路径点（锚点位置）"))
            {
                _waypoints.arraySize++;
                var el = _waypoints.GetArrayElementAtIndex(_waypoints.arraySize - 1);
                el.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
                el.FindPropertyRelative("waitSeconds").floatValue = 0.35f;
            }

            if (GUILayout.Button("添加路径点（正前方 3m）"))
            {
                var t = path.transform;
                _waypoints.arraySize++;
                var el = _waypoints.GetArrayElementAtIndex(_waypoints.arraySize - 1);
                el.FindPropertyRelative("localPosition").vector3Value =
                    t.InverseTransformPoint(t.position + t.forward * 3f);
                el.FindPropertyRelative("waitSeconds").floatValue = 0.35f;
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("使用选中物体世界坐标（转本地）"))
            {
                var sel = Selection.activeTransform;
                if (sel == null)
                    EditorUtility.DisplayDialog("巡逻路径", "请先在 Hierarchy 中选中一个用作路点的物体。", "好");
                else
                {
                    var t = path.transform;
                    _waypoints.arraySize++;
                    var el = _waypoints.GetArrayElementAtIndex(_waypoints.arraySize - 1);
                    el.FindPropertyRelative("localPosition").vector3Value = t.InverseTransformPoint(sel.position);
                    el.FindPropertyRelative("waitSeconds").floatValue = 0.35f;
                }
            }

            if (GUILayout.Button("清空路径点"))
            {
                if (EditorUtility.DisplayDialog("巡逻路径", "确定清空全部路径点？", "清空", "取消"))
                    _waypoints.ClearArray();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.PropertyField(_moveSpeed);
        EditorGUILayout.PropertyField(_turnSpeed);
        EditorGUILayout.PropertyField(_arrive);
        EditorGUILayout.PropertyField(_loop);

        serializedObject.ApplyModifiedProperties();
    }
}
