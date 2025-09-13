using UnityEngine;

public class Wait : ActionNode
{
    private float duration;
    private float startTime;

    
    
    public Wait(Transform transform, float duration) : base(transform)
    {
        this.duration = duration;
    }

    protected override NodeState DoEvaluate() {
        // Debug.Log(Time.time - startTime+" " +duration);
        if(state == NodeState.Running)
        {
            if(Time.time - startTime >= duration)
            {
                state = NodeState.Success;
                return state;
            }
        }
        else
        {
            startTime = Time.time;
            state = NodeState.Running;
        }

        return state;
    }
}
