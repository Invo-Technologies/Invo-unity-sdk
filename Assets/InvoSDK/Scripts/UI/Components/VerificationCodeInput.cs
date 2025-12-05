using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InvoSDK.UI
{
    public class VerificationCodeInput : MonoBehaviour
    {
        [SerializeField] private TMP_InputField[] codeInputs;
        public System.Action<string> OnCodeCompleted;

        private void Start()
        {
            for (int i = 0; i < codeInputs.Length; i++)
            {
                int index = i;
                var input = codeInputs[i];

                // Limit to 1 char
                input.characterLimit = 1;

                input.onValueChanged.AddListener(_ => HandleInputChanged(index));
                input.onSelect.AddListener(_ => MoveCaretToEnd(input));
            }

            // Autofocus first box
            EventSystem.current?.SetSelectedGameObject(codeInputs[0].gameObject);
        }

        private void HandleInputChanged(int index)
        {
            var current = codeInputs[index];

            // Go next if typed a char
            if (!string.IsNullOrEmpty(current.text))
            {
                if (index + 1 < codeInputs.Length)
                {
                    codeInputs[index + 1].Select();
                }
                else
                {
                    // All filled → emit event
                    string code = GetFullCode();
                    OnCodeCompleted?.Invoke(code);
                }
            }
            else
            {
                // If erased, go back
                if (index > 0)
                {
                    codeInputs[index - 1].Select();
                }
            }
        }

        private void MoveCaretToEnd(TMP_InputField input)
        {
            input.caretPosition = input.text.Length;
        }

        public string GetFullCode()
        {
            string code = "";
            foreach (var input in codeInputs)
                code += input.text;
            return code;
        }

        public void Clear()
        {
            foreach (var input in codeInputs)
                input.text = "";
            EventSystem.current?.SetSelectedGameObject(codeInputs[0].gameObject);
        }
    }
}
