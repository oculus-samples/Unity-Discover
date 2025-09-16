using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class MenuHanler : MonoBehaviour
{
    public GameObject carrot;
    public GameObject asparagus;
    public void chageToCarrot()
    {
        asparagus.SetActive(false);
        carrot.SetActive(true);
        
    }
    public void chageToAsparagus()
    {
        asparagus.SetActive(true);
        carrot.SetActive(false);

    }
}
