using UnityEngine;

public class GameUIController : MonoBehaviour
{
    private ButterflyMovement butterflyMovement;

    public void RegisterButterfly(ButterflyMovement butterfly)
    {
        butterflyMovement = butterfly;
    }

    public void ResetButterfly()
    {
        if (butterflyMovement != null)
        {
            butterflyMovement.ResetButterflyPosition();
        }
    }

    public void SetButterflySpeed(float value)
    {
        if (butterflyMovement != null)
        {
            butterflyMovement.SetMoveSpeed(value);
        }
    }

    public void SetButterflyRange(float value)
    {
        if (butterflyMovement != null)
        {
            butterflyMovement.SetMoveRange(value);
        }
    }

    public void SetButterflyDirection(int index)
    {
        if (butterflyMovement != null)
        {
            butterflyMovement.SetDirection(index);
        }
    }
}