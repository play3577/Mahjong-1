﻿using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace UI.DataBinding
{
    [RequireComponent(typeof(ToggleGroup))]
    public class EnumDataBinding : MonoBehaviour, IBindData
    {
        public ScriptableObject Target { get; set; }
        [SerializeField] private string fieldName;
        private ToggleGroup toggleGroup;
        private FieldInfo fieldInfo;
        private Type enumType;
        private int enumCount;
        private Toggle[] toggles;

        private void OnEnable()
        {
            toggleGroup = GetComponent<ToggleGroup>();
            toggleGroup.allowSwitchOff = false;
        }

        public void Apply()
        {
            if (!CheckNull()) return;
            int index = (int)fieldInfo.GetValue(Target);
            Debug.Log($"{name} applied as {fieldInfo.GetValue(Target)}");
            toggles[index].isOn = true;
        }

        public void UpdateBind()
        {
            if (!CheckNull()) return;
            var activeIndex = Array.FindIndex(toggles, t => t.isOn);
            fieldInfo.SetValue(Target, activeIndex);
        }

        private bool CheckNull()
        {
            if (Target == null)
            {
                Debug.LogError("Target should not be null");
                return false;
            }
            if (fieldInfo == null)
            {
                fieldInfo = Target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (fieldInfo == null)
                {
                    Debug.LogError($"Unknown field name: {fieldName}");
                    return false;
                }
            }
            enumType = fieldInfo.FieldType;
            enumCount = enumType.GetEnumNames().Length;
            if (transform.childCount < enumCount)
            {
                Debug.LogError($"Not enough toggle children, found {transform.childCount} require {enumCount}");
                return false;
            }
            if (toggles == null)
            {
                toggles = new Toggle[enumCount];
                for (int i = 0, count = 0; i < transform.childCount; i++)
                {
                    if (count >= toggles.Length) break;
                    var t = transform.GetChild(i).GetComponent<Toggle>();
                    if (t != null) toggles[count++] = t;
                }
            }
            return true;
        }
    }
}
