using UnityEngine;
using UnityEditor;

namespace AparatoCustomAttributes
{
    // Atributo para campos somente leitura no Inspector
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class ReadOnlyAttribute : PropertyAttribute { }
    
#if UNITY_EDITOR
    // Drawer para exibir campos ReadOnly no Editor
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Salva o estado GUI atual
            bool prevEnabled = GUI.enabled;
            GUI.enabled = false;
            
            // Desenha o campo como desabilitado
            EditorGUI.PropertyField(position, property, label, true);
            
            // Restaura o estado GUI
            GUI.enabled = prevEnabled;
        }
    }
#endif
} 