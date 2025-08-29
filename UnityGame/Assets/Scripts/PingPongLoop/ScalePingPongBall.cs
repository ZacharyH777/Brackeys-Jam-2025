using UnityEngine;

public class ScalePingPongBall : MonoBehaviour
{

    void Update()
    {
        this.transform.localScale = new Vector3((transform.localPosition.z + 15) / 60, (transform.localPosition.z + 15) / 60, 1);
    }
}
