using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DaltonismoController : MonoBehaviour
{
     public Material matColorBlindness;

    public void Normal()
    {
        if (matColorBlindness != null)
            matColorBlindness.SetFloat("_Mode", 0);
    }

    public void Protanopia()
    {
        if (matColorBlindness != null)
            matColorBlindness.SetFloat("_Mode", 1);
    }

    public void Deuteranopia()
    {
        if (matColorBlindness != null)
            matColorBlindness.SetFloat("_Mode", 2);
    }

    public void Tritanopia()
    {
        if (matColorBlindness != null)
            matColorBlindness.SetFloat("_Mode", 3);
    }
}
