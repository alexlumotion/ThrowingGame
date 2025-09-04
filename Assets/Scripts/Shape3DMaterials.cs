using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shape3DMaterials : MonoBehaviour
{

    public Material[] materials;

    public MeshRenderer[] meshRenderers;

    // Start is called before the first frame update
    void Start()
    {
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        SetMaterial();
    }

    public void SetMaterial()
    {
        int i = Random.Range(0, 5);
        for (int cnt = 0; cnt < meshRenderers.Length; cnt++)
        {
            meshRenderers[cnt].material = materials[i];
        }
    }
}
