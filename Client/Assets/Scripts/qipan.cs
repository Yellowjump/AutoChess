using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityGameFramework.Runtime;

public class qipan : MonoBehaviour
{

    public GameObject qige;
    public GameObject qige2;
    void Start()
    {
        var offsetX = -3.5f;
        var offsetZ = -3;
        for (int i = 0; i < 8; i++)
        {
            var startPosX = i % 2 == 0 ? 0.5f : 0f;
            for (int indexX = 0; indexX < 7; indexX++)
            {
                Vector3 pos = new Vector3(startPosX + indexX+offsetX, 0, offsetZ+Mathf.Sqrt(3) * i / 2);
                GameObject qg = Instantiate(qige2, pos, Quaternion.Euler(0, 0, 0));
                qg.transform.parent = this.transform;
                QiziGuanLi.Instance.qigepos[i][indexX] = pos;
            }
        }
        /*for (int i = 0; i < 9; i++)
        {
            if (i==4)
            {
                for (int j = 0; j < 5; j++)
                {
                    Vector3 pos = new Vector3(Mathf.Sqrt(2) * -2f + j * Mathf.Sqrt(2), 0, 0);
                    GameObject qg = Instantiate(qige3, pos, Quaternion.Euler(0, 45, 0));
                    qg.transform.parent = this.transform;
                    QiziGuanLi.Instance.qigepos[i][j] = pos;
                    //Log.Info("hfk:i="+i+" j="+j+" pos=" + pos);
                }
            }
            else if (i % 2 == 0)
            {
                for (int j = 0; j < 5; j++)
                {
                    Vector3 pos = new Vector3(Mathf.Sqrt(2) * -2f + j * Mathf.Sqrt(2), 0, Mathf.Sqrt(2) * 2f - i * Mathf.Sqrt(2) / 2);
                    GameObject qg = Instantiate(qige2, pos, Quaternion.Euler(0, 45, 0));
                    qg.transform.parent = this.transform;
                    QiziGuanLi.Instance.qigepos[i][j] = pos;
                    //Log.Info("hfk:i=" + i + " j=" + j + " pos=" + pos);
                }
            }
            else
            {
                for (int j = 0; j < 6; j++)
                {
                    Vector3 pos = new Vector3(Mathf.Sqrt(2) * -2.5f + j * Mathf.Sqrt(2), 0, Mathf.Sqrt(2) * 2f - i * Mathf.Sqrt(2) / 2);
                    GameObject qg = Instantiate(qige2, pos, Quaternion.Euler(0, 45, 0));
                    qg.transform.parent = this.transform;
                    QiziGuanLi.Instance.qigepos[i][j] = pos;
                    //Log.Info("hfk:i=" + i + " j=" + j + " pos=" + pos);
                }
            }
        }*/
        for (int i=0;i<9;i++)
        {
            GameObject qg = Instantiate(qige, new Vector3(-4+i,0,-4.5f),Quaternion.identity);
            GameObject qg2 = Instantiate(qige, new Vector3(-4 + i, 0, 4.5f), Quaternion.identity);
            qg.transform.parent = this.transform;
            qg2.transform.parent = this.transform;
            QiziGuanLi.instance.cxqigepos[i] = qg.transform.position;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
