using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    public class InvoStepController : MonoBehaviour
    {
        [System.Serializable]
        public class StepElement
        {
            public Image circle;
            public TMP_Text numberText;
            public TMP_Text labelText;
        }

        [Header("Steps")]
        public List<StepElement> steps = new();
        [Header("Colors")]
        public Color defaultColor = new Color(0.2f, 0.2f, 0.2f);
        public Color activeColor = new Color(0f, 0.8f, 0.8f);
        public Color completedColor = new Color(0f, 0.5f, 0.5f);

        private int _currentStep = 0;

        public void InitializeSteps(string[] labels)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                steps[i].labelText.text = i < labels.Length ? labels[i] : $"Step {i + 1}";
                SetStepState(i, StepState.Default);
            }
            SetStep(0);
        }

        public void SetStep(int index)
        {
            if (index < 0 || index >= steps.Count) return;

            _currentStep = index;
            for (int i = 0; i < steps.Count; i++)
            {
                if (i < index) SetStepState(i, StepState.Completed);
                else if (i == index) SetStepState(i, StepState.Active);
                else SetStepState(i, StepState.Default);
            }
        }

        private void SetStepState(int i, StepState state)
        {
            if (i >= steps.Count) return;

            var s = steps[i];
            switch (state)
            {
                case StepState.Default:
                    s.circle.color = defaultColor;
                    s.labelText.color = Color.gray;
                    break;
                case StepState.Active:
                    s.circle.color = activeColor;
                    s.labelText.color = Color.white;
                    break;
                case StepState.Completed:
                    s.circle.color = completedColor;
                    s.labelText.color = new Color(0.7f, 1f, 0.7f);
                    break;
            }
        }

        private enum StepState { Default, Active, Completed }
    }
}
