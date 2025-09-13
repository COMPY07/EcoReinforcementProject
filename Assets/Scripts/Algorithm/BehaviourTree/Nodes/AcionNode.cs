using UnityEngine;

public abstract class ActionNode : Node
{
    protected Transform transform;
    protected Animator animator;
    protected Rigidbody rigidbody;
    protected bool isActive = false;
    protected bool mustComplete = false;
    protected string cooldownKey = null;
    public ActionNode(Transform transform, bool forceComplete = false, string cooldownKey = null)
    {
        
        this.transform = transform;
        this.mustComplete = forceComplete;
        this.cooldownKey = cooldownKey;
        animator = transform.GetComponent<Animator>();
        rigidbody = transform.GetComponent<Rigidbody>();
    }
    protected abstract override NodeState DoEvaluate();
    public sealed override NodeState Evaluate()
    { if (mustComplete && isActive && state == NodeState.Running) {
            NodeState result = DoEvaluate();
            if (wasRunning && result != NodeState.Running) {
                OnExitNode();
                isActive = false;
            }
            if (result == NodeState.Success && !string.IsNullOrEmpty(cooldownKey)) {
                Cooldown.StartCooldown(cooldownKey);
            }
            state = result;
            wasRunning = (state == NodeState.Running);
            return result;
        }
        NodeState evaluationResult = base.Evaluate();
        if (evaluationResult == NodeState.Success && !string.IsNullOrEmpty(cooldownKey)) {
            Cooldown.StartCooldown(cooldownKey);
        }
        
        return evaluationResult;
    }

    protected override void OnEnterNode()
    {
        base.OnEnterNode();
        SetActive(true);
    }

    protected override void OnExitNode()
    {
        base.OnExitNode();
        SetActive(false);
    }

    protected void SetActive(bool active)
    {
        isActive = active;
    }
    public override void FixedEvaluate()
    {

    }
}