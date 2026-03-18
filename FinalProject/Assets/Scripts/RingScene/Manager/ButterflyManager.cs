using System.Collections.Generic;
using UnityEngine;

public class ButterflyUIManager : MonoBehaviour
{
    [Header("Butterfly Groups")]
    [SerializeField] private List<GameObject> leftButterflies = new List<GameObject>();
    [SerializeField] private List<GameObject> centerButterflies = new List<GameObject>();
    [SerializeField] private List<GameObject> rightButterflies = new List<GameObject>();

    [Header("Training Settings")]
    [SerializeField] private bool trainLeftSide = true;
    [SerializeField][Range(0f, 1f)] private float neglectedSideBias = 0.8f;

    private GameObject currentButterfly;

    private void Start()
    {
        HideAll();
        ShowNextButterfly();
    }

    public void HideAll()
    {
        foreach (GameObject butterfly in leftButterflies)
        {
            if (butterfly != null) butterfly.SetActive(false);
        }

        foreach (GameObject butterfly in centerButterflies)
        {
            if (butterfly != null) butterfly.SetActive(false);
        }

        foreach (GameObject butterfly in rightButterflies)
        {
            if (butterfly != null) butterfly.SetActive(false);
        }
    }

    public void ShowNextButterfly()
    {
        HideAll();

        List<GameObject> chosenGroup = ChooseGroup();

        if (chosenGroup == null || chosenGroup.Count == 0)
        {
            Debug.LogWarning("No butterflies available in chosen group.");
            currentButterfly = null;
            return;
        }

        int randomIndex = Random.Range(0, chosenGroup.Count);
        currentButterfly = chosenGroup[randomIndex];

        if (currentButterfly != null)
        {
            currentButterfly.SetActive(true);
        }
    }

    private List<GameObject> ChooseGroup()
    {
        bool chooseNeglectedSide = Random.value < neglectedSideBias;

        if (trainLeftSide)
        {
            if (chooseNeglectedSide && leftButterflies.Count > 0)
                return leftButterflies;

            if (centerButterflies.Count > 0)
                return centerButterflies;

            if (rightButterflies.Count > 0)
                return rightButterflies;
        }
        else
        {
            if (chooseNeglectedSide && rightButterflies.Count > 0)
                return rightButterflies;

            if (centerButterflies.Count > 0)
                return centerButterflies;

            if (leftButterflies.Count > 0)
                return leftButterflies;
        }

        return null;
    }

    public RectTransform GetCurrentButterflyRect()
    {
        if (currentButterfly == null)
            return null;

        return currentButterfly.GetComponent<RectTransform>();
    }

    public void SetTrainingSide(bool leftSide)
    {
        trainLeftSide = leftSide;
    }
}