
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class MatrixLED8x8 : UdonSharpBehaviour
{
    public MeshRenderer[] m_MeshRenderers;
    public Material m_LEDOnMaterial;
    public Material m_LEDOffMaterial;
}
