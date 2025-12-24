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

    [Header("SE設定")]
    [SerializeField, Tooltip("クリア時に再生するSE")]
    private AudioClip clearSe;
    [SerializeField, Tooltip("失敗時に再生するSE")]
    private AudioClip failSe;
    [SerializeField, Tooltip("SEを鳴らすAudioSource（1つ）")]
    private AudioSource seSource;
    [SerializeField, Range(0f, 50f), Tooltip("クリアSEの音量スケール (PlayOneShot volumeScale)")]
    private float clearSeVolume = 1f;
    [SerializeField, Range(0f, 50f), Tooltip("失敗SEの音量スケール (PlayOneShot volumeScale)")]
    private float failSeVolume = 1f;

    private bool isFinished = false;
    private bool clearSePlayed = false;
    private bool failSePlayed = false;

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
                    PlayClearSeOnce();
                }
                else if (p.failed)
                {
                    isFinished = true;
                    label.text = failText;
                    PlayFailSeOnce();
                }
                else if (!isFinished)
                {
                    string s = string.Format(format, p.maxPercent);
                    if (!p.lengthOk) s += lengthNgSuffix;
                    label.text = s;
                }
            })
            .AddTo(this);

        // 失敗通知を直接受けてSEを鳴らす（表示更新に先行する場合の保険）
        judge.OnFailedAsObservable
            .Subscribe(_ => PlayFailSeOnce())
            .AddTo(this);

        // クリア通知を直接受けてSEを鳴らす（表示更新に先行する場合の保険）
        judge.OnClearedAsObservable
            .Subscribe(_ => PlayClearSeOnce())
            .AddTo(this);
    }

    private void PlayClearSeOnce()
    {
        if (clearSePlayed) return;
        clearSePlayed = true;
        if (seSource != null && clearSe != null)
        {
            seSource.PlayOneShot(clearSe, clearSeVolume);
        }
    }

    private void PlayFailSeOnce()
    {
        if (failSePlayed) return;
        failSePlayed = true;
        if (seSource != null && failSe != null)
        {
            seSource.PlayOneShot(failSe, failSeVolume);
        }
    }
}
