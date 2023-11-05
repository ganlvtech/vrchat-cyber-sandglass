using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using Random = UnityEngine.Random;

public class 赛博沙漏 : UdonSharpBehaviour
{
    // 父物体（即 VRC Pickup 所在 GameObject，用于检测 Owner）
    public GameObject m_ParentGameObject;

    // 上方 LED 矩阵
    public MatrixLED8x8 m_LED1;

    // 下方 LED 矩阵
    public MatrixLED8x8 m_LED2;

    // 上方 LED 矩阵亮灭数据，固定为 64 个灯
    [UdonSynced] public ulong led1;

    // 下方 LED 矩阵亮灭数据，固定为 64 个灯
    [UdonSynced] public ulong led2;

    // 沙子自由下落的更新间隔
    public float m_UpdateSandInterval = 0.3f;

    // 沙漏中间沙子下落一粒的间隔
    public float m_DropSandInterval = 1.0f;

    // 触发上下甩动和左右晃动时的加速度阈值（推荐设为 3.0）
    public float m_MinAcceleration = 3.0f;

    // 重力加速度（推荐设为 4.0 而不是 9.81，因为 9.81 太难触发上下甩动了）
    public float m_Gravity = 9.81f;

    private float prevUpdateSandTime;
    private float prevDropSandTime;
    private Vector3 prevPosition;
    private Vector3 prevVelocity;
    private float prevTime;
    private int[][][] INDEX_MAP;

    void Start()
    {
        INDEX_MAP = new[]
        {
            // 0 正放
            new[]
            {
                new[] { 63 },
                new[] { 55, 62 },
                new[] { 47, 54, 61 },
                new[] { 39, 46, 53, 60 },
                new[] { 31, 38, 45, 52, 59 },
                new[] { 23, 30, 37, 44, 51, 58 },
                new[] { 15, 22, 29, 36, 43, 50, 57 },
                new[] { 7, 14, 21, 28, 35, 42, 49, 56 },
                new[] { 6, 13, 20, 27, 34, 41, 48 },
                new[] { 5, 12, 19, 26, 33, 40 },
                new[] { 4, 11, 18, 25, 32 },
                new[] { 3, 10, 17, 24 },
                new[] { 2, 9, 16 },
                new[] { 1, 8 },
                new[] { 0 },
            },
            // 1 左倒
            new[]
            {
                new[] { 56 },
                new[] { 48, 57 },
                new[] { 40, 49, 58 },
                new[] { 32, 41, 50, 59 },
                new[] { 24, 33, 42, 51, 60 },
                new[] { 16, 25, 34, 43, 52, 61 },
                new[] { 8, 17, 26, 35, 44, 53, 62 },
                new[] { 0, 9, 18, 27, 36, 45, 54, 63 },
                new[] { 1, 10, 19, 28, 37, 46, 55 },
                new[] { 2, 11, 20, 29, 38, 47 },
                new[] { 3, 12, 21, 30, 39 },
                new[] { 4, 13, 22, 31 },
                new[] { 5, 14, 23 },
                new[] { 6, 15 },
                new[] { 7 },
            },
            // 2 上下翻转
            new[]
            {
                new[] { 0 },
                new[] { 1, 8 },
                new[] { 2, 9, 16 },
                new[] { 3, 10, 17, 24 },
                new[] { 4, 11, 18, 25, 32 },
                new[] { 5, 12, 19, 26, 33, 40 },
                new[] { 6, 13, 20, 27, 34, 41, 48 },
                new[] { 7, 14, 21, 28, 35, 42, 49, 56 },
                new[] { 15, 22, 29, 36, 43, 50, 57 },
                new[] { 23, 30, 37, 44, 51, 58 },
                new[] { 31, 38, 45, 52, 59 },
                new[] { 39, 46, 53, 60 },
                new[] { 47, 54, 61 },
                new[] { 55, 62 },
                new[] { 63 },
            },
            // 3 右倒
            new[]
            {
                new[] { 7 },
                new[] { 6, 15 },
                new[] { 5, 14, 23 },
                new[] { 4, 13, 22, 31 },
                new[] { 3, 12, 21, 30, 39 },
                new[] { 2, 11, 20, 29, 38, 47 },
                new[] { 1, 10, 19, 28, 37, 46, 55 },
                new[] { 0, 9, 18, 27, 36, 45, 54, 63 },
                new[] { 8, 17, 26, 35, 44, 53, 62 },
                new[] { 16, 25, 34, 43, 52, 61 },
                new[] { 24, 33, 42, 51, 60 },
                new[] { 32, 41, 50, 59 },
                new[] { 40, 49, 58 },
                new[] { 48, 57 },
                new[] { 56 },
            },
        };
        led1 = 0xffffffffffffffff;
    }

    private void Update()
    {
        if (Networking.IsOwner(m_ParentGameObject) && !Networking.IsOwner(gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        var time = Time.time;
        if (Networking.IsOwner(gameObject))
        {
            // 如果自己是 Owner 的话，自己计算沙子下落

            // 获取加速度
            var direction = -1;
            var transform1 = transform;
            var position = transform1.position;
            var velocity = (position - prevPosition) / (time - prevTime);
            var linearAcceleration = (velocity - prevVelocity) / (time - prevTime);
            prevPosition = position;
            prevVelocity = velocity;
            prevTime = time;

            // 计算方向
            var acceleration = linearAcceleration + new Vector3(0, -m_Gravity, 0);
            var up = transform1.up;
            var right = transform1.right;
            var upAcceleration = Vector3.Dot(up, acceleration);
            var rightAcceleration = Vector3.Dot(right, acceleration);
            if (upAcceleration < -m_MinAcceleration)
            {
                direction = 0;
            }
            else if (upAcceleration > m_MinAcceleration)
            {
                direction = 2;
            }
            else if (rightAcceleration < -m_MinAcceleration)
            {
                direction = 1;
            }
            else if (rightAcceleration > m_MinAcceleration)
            {
                direction = 3;
            }
            else
            {
                direction = -1;
            }

            // 更新沙子
            if (time - prevUpdateSandTime > m_UpdateSandInterval)
            {
                prevUpdateSandTime = time;

                var led1IsOn = DeserializeLED(led1);
                var led2IsOn = DeserializeLED(led2);
                var updated = false;
                switch (direction)
                {
                    case 0:
                        if (UpdateLEDMatrix(direction, led2IsOn))
                        {
                            updated = true;
                        }
                        if (time - prevDropSandTime > m_DropSandInterval)
                        {
                            if (led1IsOn[63] && !led2IsOn[0])
                            {
                                led1IsOn[63] = false;
                                led2IsOn[0] = true;
                                updated = true;
                                prevDropSandTime = time;
                            }
                        }
                        if (UpdateLEDMatrix(direction, led1IsOn))
                        {
                            updated = true;
                        }
                        break;
                    case 2:
                        if (UpdateLEDMatrix(direction, led1IsOn))
                        {
                            updated = true;
                        }
                        if (time - prevDropSandTime > m_DropSandInterval)
                        {
                            if (!led1IsOn[63] && led2IsOn[0])
                            {
                                led1IsOn[63] = true;
                                led2IsOn[0] = false;
                                updated = true;
                                prevDropSandTime = time;
                            }
                        }
                        if (UpdateLEDMatrix(direction, led2IsOn))
                        {
                            updated = true;
                        }
                        break;
                    case 1:
                    case 3:
                        if (UpdateLEDMatrix(direction, led1IsOn))
                        {
                            updated = true;
                        }
                        if (UpdateLEDMatrix(direction, led2IsOn))
                        {
                            updated = true;
                        }
                        break;
                }
                if (updated)
                {
                    for (int i = 0; i < led1IsOn.Length; i++) m_LED1.m_MeshRenderers[i].sharedMaterial = led1IsOn[i] ? m_LED1.m_LEDOnMaterial : m_LED1.m_LEDOffMaterial;
                    for (int i = 0; i < led2IsOn.Length; i++) m_LED2.m_MeshRenderers[i].sharedMaterial = led2IsOn[i] ? m_LED2.m_LEDOnMaterial : m_LED2.m_LEDOffMaterial;
                    led1 = SerializeLED(led1IsOn);
                    led2 = SerializeLED(led2IsOn);
                    RequestSerialization();
                }
            }
        }
        else
        {
            // 如果自己不是 Owner 的话，从 UdonSynced 的状态直接显示沙子位置
            if (time - prevUpdateSandTime > m_UpdateSandInterval)
            {
                prevUpdateSandTime = time;

                var led1IsOn = DeserializeLED(led1);
                var led2IsOn = DeserializeLED(led2);
                for (int i = 0; i < led1IsOn.Length; i++) m_LED1.m_MeshRenderers[i].sharedMaterial = led1IsOn[i] ? m_LED1.m_LEDOnMaterial : m_LED1.m_LEDOffMaterial;
                for (int i = 0; i < led2IsOn.Length; i++) m_LED2.m_MeshRenderers[i].sharedMaterial = led2IsOn[i] ? m_LED2.m_LEDOnMaterial : m_LED2.m_LEDOffMaterial;
            }
        }
    }

    private bool[] DeserializeLED(ulong s)
    {
        var result = new bool[64];
        for (int i = 0; i < 8; i++)
        {
            var x = (s >> (8 * i)) & 0xff;
            result[8 * i + 0] = (x & 0x80) != 0;
            result[8 * i + 1] = (x & 0x40) != 0;
            result[8 * i + 2] = (x & 0x20) != 0;
            result[8 * i + 3] = (x & 0x10) != 0;
            result[8 * i + 4] = (x & 0x08) != 0;
            result[8 * i + 5] = (x & 0x04) != 0;
            result[8 * i + 6] = (x & 0x02) != 0;
            result[8 * i + 7] = (x & 0x01) != 0;
        }
        return result;
    }

    private ulong SerializeLED(bool[] isOn)
    {
        ulong result = 0;
        for (int i = 0; i < 8; i++)
        {
            result |= (ulong)((isOn[8 * i + 0] ? 0x80 : 0)
                              | (isOn[8 * i + 1] ? 0x40 : 0)
                              | (isOn[8 * i + 2] ? 0x20 : 0)
                              | (isOn[8 * i + 3] ? 0x10 : 0)
                              | (isOn[8 * i + 4] ? 0x08 : 0)
                              | (isOn[8 * i + 5] ? 0x04 : 0)
                              | (isOn[8 * i + 6] ? 0x02 : 0)
                              | (isOn[8 * i + 7] ? 0x01 : 0)) << (8 * i);
        }
        return result;
    }

    /// <summary>
    /// 更新一个 LED 矩阵中的沙子状态
    /// 从下往上遍历，如果左上和右上都有沙子，则随机挑一个下落
    /// 如果左上和右上有一个沙子，则这个沙子下落
    /// 然后判断正上方的沙子竖直下落
    /// </summary>
    /// <param name="direction">方向 0 正放 1 左倒 2 上下翻转 3 右倒</param>
    /// <param name="isOn">LED 是否开启</param>
    /// <returns>是否已更新沙子状态</returns>
    private bool UpdateLEDMatrix(int direction, bool[] isOn)
    {
        var updated = false;
        foreach (var layer in INDEX_MAP[direction])
        {
            Utilities.ShuffleArray(layer);
            foreach (var i in layer)
            {
                if (!isOn[i])
                {
                    var upperLeftIndex = GetNeighbor(i, direction, 7);
                    var upperRightIndex = GetNeighbor(i, direction, 1);
                    var upperIndex = GetNeighbor(i, direction, 0);
                    if (upperLeftIndex >= 0 && upperRightIndex >= 0 && isOn[upperLeftIndex] && isOn[upperRightIndex])
                    {
                        if (Random.value < 0.5)
                        {
                            isOn[upperLeftIndex] = false;
                            isOn[i] = true;
                            updated = true;
                        }
                        else
                        {
                            isOn[upperRightIndex] = false;
                            isOn[i] = true;
                            updated = true;
                        }
                    }
                    else if (upperLeftIndex >= 0 && isOn[upperLeftIndex])
                    {
                        isOn[upperLeftIndex] = false;
                        isOn[i] = true;
                        updated = true;
                    }
                    else if (upperRightIndex >= 0 && isOn[upperRightIndex])
                    {
                        isOn[upperRightIndex] = false;
                        isOn[i] = true;
                        updated = true;
                    }
                    else if (upperIndex >= 0 && isOn[upperIndex])
                    {
                        isOn[upperIndex] = false;
                        isOn[i] = true;
                        updated = true;
                    }
                }
            }
        }
        return updated;
    }

    /// <summary>
    /// 获取相邻格子的下标，如果超出范围返回 -1
    /// </summary>
    /// <param name="i">当前格子下标</param>
    /// <param name="direction">方向 0 正放 1 左倒 2 上下翻转 3 右倒</param>
    /// <param name="neighborDirection">邻居方向 0 上 1 右上 2 右 3 右下 4 下 5 左下 6 左 7 左上</param>
    /// <returns></returns>
    private int GetNeighbor(int i, int direction, int neighborDirection)
    {
        var x = i % 8;
        var y = i / 8;
        var realDirection = (direction * 2 + neighborDirection) % 8;
        switch (realDirection)
        {
            case 0:
                x -= 1;
                y -= 1;
                break;
            case 1:
                y -= 1;
                break;
            case 2:
                x += 1;
                y -= 1;
                break;
            case 3:
                x += 1;
                break;
            case 4:
                x += 1;
                y += 1;
                break;
            case 5:
                y += 1;
                break;
            case 6:
                x -= 1;
                y += 1;
                break;
            case 7:
                x -= 1;
                break;
        }

        if (x < 0 || y < 0 || x >= 8 || y >= 8)
        {
            return -1;
        }
        return y * 8 + x;
    }
}