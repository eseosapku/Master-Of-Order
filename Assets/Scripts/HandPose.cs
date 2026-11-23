using UnityEngine;
using UnityEngine.InputSystem;

public class HandPose : MonoBehaviour
{
    private static readonly int GripId = Animator.StringToHash("Grip");
    private static readonly int TriggerId = Animator.StringToHash("Trigger");

    [SerializeField] private InputActionReference grip;
    [SerializeField] private InputActionReference trigger;
    [SerializeField] private Animator animator;
    [SerializeField] private float damping = 0.05f;

    private void OnEnable()
    {
        if (grip != null) grip.action.Enable();
        if (trigger != null) trigger.action.Enable();
    }

    private void Update()
    {
        float g = grip != null ? grip.action.ReadValue<float>() : 0f;
        float t = trigger != null ? trigger.action.ReadValue<float>() : 0f;
        animator.SetFloat(GripId, g, damping, Time.deltaTime);
        animator.SetFloat(TriggerId, t, damping, Time.deltaTime);
    }
}
