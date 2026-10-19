using UnityEngine;

public class ActivateObjectOnStart : MonoBehaviour
{
    [Header("Object To Activate")]
    [SerializeField] private GameObject targetObject;

    private void Start()
    {
        if (targetObject == null)
        {
            Debug.LogWarning("No target object assigned.");
            return;
        }

        targetObject.SetActive(true);
        Debug.Log($"{targetObject.name} activated on Start.");
    }
}