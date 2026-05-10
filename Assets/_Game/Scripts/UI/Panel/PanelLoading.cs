using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelLoading : UICanvas
{
    [SerializeField] private Image progressFillImage;
    [SerializeField] private TMP_Text progressText;

    public override void SetUp()
    {
        base.SetUp();
        BindReferences();
        SetProgress(0f);
    }

    public override void Open()
    {
        BindReferences();
        base.Open();
    }

    public void SetProgress(float progress)
    {
        BindReferences();

        progress = Mathf.Clamp01(progress);

        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = progress;
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(progress * 100f)}%";
        }
    }

    private void BindReferences()
    {
        if (progressFillImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            foreach (Image image in images)
            {
                if (image != null && image.type == Image.Type.Filled)
                {
                    progressFillImage = image;
                    break;
                }
            }
        }

        if (progressText == null)
        {
            progressText = GetComponentInChildren<TMP_Text>(true);
        }
    }
}
