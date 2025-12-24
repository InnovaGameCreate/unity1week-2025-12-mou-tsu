using UnityEngine;
using UniRx;
using TMPro;

public class FitRateTextPresenterRx : MonoBehaviour
{
    [SerializeField] private StickFitJudgeRx judge;
    [SerializeField] private TMP_Text label;

    [Header("通常時の表示フォーマット")]
    [SerializeField] private string format = "ハマリ率: {0}%";
    [SerializeField] private string lengthNgSuffix = "（長さNG）";

    [Header("クリア時の表示")]
    [SerializeField] private string clearText = "CLEAR!";

    [Header("失敗時の表示")]
    [SerializeField] private string failText = "FAILED...";

    private bool isFinished = false;

    void Start()
    {
        if (judge == null || label == null) return;

        judge.OnProgressAsObservable
            .Subscribe(p =>
            {
                if (p.cleared)
                {
                    isFinished = true;
                    label.text = clearText;
                }
                else if (p.failed)
                {
                    isFinished = true;
                    label.text = failText;
                }
                else if (!isFinished)
                {
                    string s = string.Format(format, p.maxPercent);
                    if (!p.lengthOk) s += lengthNgSuffix;
                    label.text = s;
                }
            })
            .AddTo(this);
    }
}
