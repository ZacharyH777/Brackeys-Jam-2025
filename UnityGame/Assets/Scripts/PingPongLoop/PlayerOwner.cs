using UnityEngine;

/*
* Tags a GameObject with a player id.
* Put this on the same object that holds PaddleKinematics (paddle root).
*/
[DisallowMultipleComponent]
public sealed class PlayerOwner : MonoBehaviour
{
    [Header("Owner")]
    [Tooltip("Player id")]
    public PlayerId player_id = PlayerId.P1;
}