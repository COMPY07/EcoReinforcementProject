using UnityEngine;

public class Food : MonoBehaviour
{
        public int nutrition;

        public float regenTime = 5f;
        private float currentTime;


        private Renderer[] renderer;
        private Collider collider;

        private bool currentState;
        
        private void StateChange(bool state)
        {
                for(int i  = 0; i < renderer.Length; i++)
                        renderer[i].enabled = state;
                collider.enabled = state;
                currentState = state;
        }
        public void Start()
        {
                if (transform.childCount == 0)
                {
                        renderer = new Renderer[1] { transform.GetComponent<Renderer>() };
                }
                else
                {
                        renderer = new Renderer[transform.childCount];
                        for (int i = 0; i < transform.childCount; i++)
                        {
                                renderer[i] = transform.GetChild(i).GetComponent<Renderer>();
                        }
                }
                collider = GetComponent<Collider>();
                currentTime = 0f;
        }
        public void Update()
        {
                if (currentState) return;
                currentTime += Time.deltaTime;

                if (regenTime <= currentTime)
                {
                        StateChange(true);
                }

        }
        
        
        public float Eat() {
                StateChange(false);
                currentTime = 0;
                return nutrition;
        }
        
        
        
}