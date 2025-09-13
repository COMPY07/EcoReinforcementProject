using System.Collections.Generic;
using UnityEngine;

public class IsInRangeTag : ActionNode
{
    private string tag;
    private float distance;
    private List<Transform> targets;
    private string layer;
    public Transform nearObject;
    private BlackBoard back;
    public IsInRangeTag(Transform transform, string tag = "Default", string layer = "Entity",
        float distance = 10f, bool forceComplete = false, 
        string cooldownKey = null) : base(transform, forceComplete, cooldownKey)
    {
        this.tag = tag;
        this.distance = distance;
        this.layer = layer;
        targets = new List<Transform>();

        back = transform.GetComponent<AlgorithmBaseEntity>().GetBlack();
    }

    protected override NodeState DoEvaluate(){
        Search();
        // if(tag == "Food") Debug.Log(nearObject);
        if (nearObject == null || Vector3.Distance(transform.position, nearObject.position) > distance)
            return NodeState.Failure;
        
        return NodeState.Success;
    }

    private void Search(){
        targets.Clear();
        Collider[] colliders = Physics.OverlapSphere(transform.position, distance, LayerMask.GetMask(layer));

        nearObject = null;
        foreach (Collider t in colliders)
        {
            if (!t.CompareTag(tag)) continue;

            if (nearObject == null) nearObject = t.transform;
            else
            {

                if (Vector3.Distance(nearObject.position, transform.position) >
                    Vector3.Distance(t.transform.position, transform.position))
                {
                    nearObject = t.transform;
                }
            }
        }
        
        
        back.Set(tag, nearObject);
        
        
    }
}