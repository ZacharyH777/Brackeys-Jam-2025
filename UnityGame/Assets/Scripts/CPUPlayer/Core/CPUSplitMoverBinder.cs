using UnityEngine;

/*
* Small glue to wire the split controllers into CPUPlayer.
* Add to the same root as CPUPlayer or call from your setup.
* @param cpu_player The CPU player to wire
*/
[DisallowMultipleComponent]
public sealed class CPUSplitMoverBinder : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("CPU player root")]
    public CPUPlayer cpu_player;

    [Tooltip("Body follow on root")]
    public CPUBodyFollow body_follow;

    [Tooltip("Hand striker on hand")]
    public CPUHandStriker hand_striker;

    [Tooltip("Center line of table")]
    public Transform center_line;

    [Tooltip("Aim target on far side")]
    public Transform center_target;

    /*
    * Auto wire on start if fields are missing.
    * @param none
    */
    void Start()
    {
        if (cpu_player == null)
        {
            cpu_player = GetComponentInParent<CPUPlayer>();
        }

        if (cpu_player == null)
        {
            Debug.LogWarning("CPUSplitMoverBinder could not find CPUPlayer");
            return;
        }

        if (body_follow == null)
        {
            body_follow = cpu_player.GetComponent<CPUBodyFollow>();
            if (body_follow == null)
            {
                body_follow = cpu_player.gameObject.AddComponent<CPUBodyFollow>();
            }
        }

        if (hand_striker == null)
        {
            hand_striker = GetComponentInChildren<CPUHandStriker>();
            if (hand_striker == null)
            {
                Debug.LogWarning("CPUSplitMoverBinder could not find CPUHandStriker");
            }
        }

        if (center_line == null)
        {
            center_line = cpu_player.center_line;
        }

        WireRefs();
    }

    /*
    * Connect shared refs across components.
    * @param none
    */
    public void WireRefs()
    {
        if (body_follow != null)
        {
            body_follow.ball = cpu_player.ball;
            body_follow.center_line = center_line;
            body_follow.split_by_y = cpu_player.split_by_y;
        }

        if (hand_striker != null)
        {
            hand_striker.ball = cpu_player.ball;
            hand_striker.center_line = center_line;
            hand_striker.center_target = center_target;
            hand_striker.split_by_y = cpu_player.split_by_y;
        }
    }
}
