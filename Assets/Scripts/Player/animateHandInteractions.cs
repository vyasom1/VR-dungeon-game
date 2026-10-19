using UnityEngine;
using UnityEngine.InputSystem;
public class animateHandInteractions : MonoBehaviour
{
    public InputActionProperty handTrigger;
    public InputActionProperty handGrip;
    public Animator handAnimator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float triggerValue = handTrigger.action.ReadValue<float>();
        float gripValue = handGrip.action.ReadValue<float>();   
        handAnimator.SetFloat("Trigger", triggerValue); 
        handAnimator.SetFloat("Grip", gripValue);
        
    }
}
