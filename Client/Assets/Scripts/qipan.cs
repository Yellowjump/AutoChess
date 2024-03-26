using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class qipan : MonoBehaviour
{

    public GameObject qige;
    public GameObject qige2;
    public GameObject qige3;
    void Start()
    {
        for (int i=0;i<6;i++)
        {
            for (int j=0;j<4;j++)
            {
                GameObject qg =Instantiate(qige2,new Vector3(Mathf.Sqrt(2)*2.5f - i * Mathf.Sqrt(2), 0, Mathf.Sqrt(2) * 1.5f - j * Mathf.Sqrt(2)),Quaternion.Euler(0,45,0));
                qg.transform.parent = this.transform;
            }
        }
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (j != 2)
                {
                    GameObject qg = Instantiate(qige, new Vector3(Mathf.Sqrt(2) * 2 - i * Mathf.Sqrt(2), 0, Mathf.Sqrt(2) * 2 - j * Mathf.Sqrt(2)), Quaternion.Euler(0, 45, 0));
                    qg.transform.parent = this.transform;
                }
                else
                {
                    GameObject qg = Instantiate(qige3, new Vector3(Mathf.Sqrt(2) * 2 - i * Mathf.Sqrt(2), 0, Mathf.Sqrt(2) * 2 - j * Mathf.Sqrt(2)), Quaternion.Euler(0, 45, 0));
                    qg.transform.parent = this.transform;
                }
                
            }
        }
        for (int i=0;i<9;i++)
        {
            GameObject qg = Instantiate(qige, new Vector3(-4+i,0,-4.5f),Quaternion.identity);
            GameObject qg2 = Instantiate(qige, new Vector3(-4 + i, 0, 4.5f), Quaternion.identity);
            qg.transform.parent = this.transform;
            qg2.transform.parent = this.transform;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
