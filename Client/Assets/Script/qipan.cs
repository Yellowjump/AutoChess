using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class qipan : MonoBehaviour
{

    public Object qige;
    public Object qige2;
    public Object qige3;
    void Start()
    {
        for (int i=0;i<6;i++)
        {
            for (int j=0;j<4;j++)
            {
                Instantiate(qige2,new Vector3(Mathf.Sqrt(2)*2.5f - i * Mathf.Sqrt(2), 0, Mathf.Sqrt(2) * 1.5f - j * Mathf.Sqrt(2)),Quaternion.Euler(0,45,0));
            }
        }
        for (int i = 0; i < 7; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (j != 2)
                {
                    Instantiate(qige, new Vector3(Mathf.Sqrt(2) * 3 - i * Mathf.Sqrt(2), 0, Mathf.Sqrt(2) * 2 - j * Mathf.Sqrt(2)), Quaternion.Euler(0, 45, 0));
                }
                else
                {
                    Instantiate(qige3, new Vector3(Mathf.Sqrt(2) * 3 - i * Mathf.Sqrt(2), 0, Mathf.Sqrt(2) * 2 - j * Mathf.Sqrt(2)), Quaternion.Euler(0, 45, 0));
                }
                
            }
        }
        for (int i=0;i<9;i++)
        {
            Instantiate(qige, new Vector3(-4+i,0,-4.5f),Quaternion.identity);            
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
