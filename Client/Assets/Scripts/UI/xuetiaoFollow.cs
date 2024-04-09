using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class xuetiaoFollow : MonoBehaviour
{
    void Update()
    {
        transform.rotation = Quaternion.Euler(new Vector3(80, 0, 0));
    }
}
