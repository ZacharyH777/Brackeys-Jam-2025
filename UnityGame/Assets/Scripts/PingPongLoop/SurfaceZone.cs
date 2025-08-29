using System;
using UnityEngine;

/*
* Marks a collider zone with a role so the ball can react.
* Attach to non-trigger colliders for table, floor, and paddles.
*/
[DisallowMultipleComponent]
public sealed class SurfaceZone : MonoBehaviour
{
    public enum ZoneType { Table, Floor, Paddle }

    [Header("Zone")]
    [Tooltip("Type of this zone")]
    public ZoneType zone_type = ZoneType.Table;
}
