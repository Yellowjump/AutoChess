using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class DamageNumber : MonoBehaviour
{
    private Transform Point;      //UI生成位置
    private int DamageNum;
    private Text damageNumText;
    private int sizenow;
    CanvasGroup canvasGroup;

    public void Init(int type,Transform Position, int DamageNum)//tpye：1为物理伤害，2为魔法伤害，3为真实伤害
    {
        Point = Position;
        Canvas canvas = GameObject.Find("WorldCanvas").GetComponent<Canvas>();
        if (canvas != null)
        {
            gameObject.transform.forward = Camera.main.transform.forward;
            gameObject.transform.SetParent(canvas.transform);
            damageNumText = gameObject.GetComponent<Text>();
            sizenow= damageNumText.fontSize;
            canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
        gameObject.transform.position = Point.position;
        damageNumText.text= DamageNum.ToString();
        switch (type)
        {
            case 1:
                damageNumText.color = Color.yellow; 
                break;
            case 2:
                damageNumText.color = Color.blue;
                break;
            case 3:
                damageNumText.color = Color.white;
                break;
            default:
                break;
        }
        StartCoroutine(FadeOutAndDestroy(gameObject, 0.8f));
    }
    private IEnumerator FadeOutAndDestroy(GameObject damageText, float delay)
    {
        damageNumText.fontSize += 20;
        float elapsed = 0f;
        Vector3 startPosition = damageText.transform.position;
        while (elapsed < delay)
        {
            if (damageNumText.fontSize > sizenow)
            {
                damageNumText.fontSize --;
            }
            damageText.transform.position = startPosition + new Vector3(elapsed/3, elapsed*1.5f,0);
            if (Point!=null)
            {
                damageText.transform.position = new Vector3(damageText.transform.position.x, damageText.transform.position.y, Point.transform.position.z);
            }
            canvasGroup.alpha = Mathf.Lerp(1,0,elapsed/delay);
            elapsed += Time.deltaTime;
            yield return null;
        }
        //放回对象池
        if (gameObject != null)
        {
            string path = "Assets/Prefeb/DamageNumber/DamageNumber.prefab";
            GameEntry.HeroManager.ReleaseGameObject(path, gameObject);
        }
        else
        {
            Destroy(this);
        }
        yield break;
    }
}
