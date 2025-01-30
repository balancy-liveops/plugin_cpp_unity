using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Balancy.CheatPanel
{
    public class TimeCheatPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text offsetText;
        [SerializeField] private Button btnDec10min;
        [SerializeField] private Button btnInc10min;
        [SerializeField] private Button btnDec1hour;
        [SerializeField] private Button btnInc1hour;
        [SerializeField] private Button btnDec1day;
        [SerializeField] private Button btnInc1day;
        [SerializeField] private Button btnReset;

        private int m_TimeOffset = 0;

        private void Awake()
        {
            btnDec10min.onClick.AddListener(() => ChangeTime(-600));
            btnInc10min.onClick.AddListener(() => ChangeTime(600));
            btnDec1hour.onClick.AddListener(() => ChangeTime(-3600));
            btnInc1hour.onClick.AddListener(() => ChangeTime(3600));
            btnDec1day.onClick.AddListener(() => ChangeTime(-86400));
            btnInc1day.onClick.AddListener(() => ChangeTime(86400));
            btnReset.onClick.AddListener(() => ChangeTime(-m_TimeOffset));
        }

        private void ChangeTime(int seconds)
        {
            m_TimeOffset += seconds;
            offsetText.text = $"{m_TimeOffset / 3600}h {m_TimeOffset % 3600 / 60}m";
            Balancy.API.SetTimeCheatingOffset(m_TimeOffset);
        }
    }
}