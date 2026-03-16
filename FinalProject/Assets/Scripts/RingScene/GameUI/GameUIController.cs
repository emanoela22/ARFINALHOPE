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
}