using System.Collections.Generic;
using System;

namespace FSM
{
    public class StateMachine<T>
    {
        private T owner;
        private IState<T> currentState;
        private IState<T> previousState;
        private Dictionary<Type, IState<T>> states;

        public StateMachine(T owner)
        {
            states = new Dictionary<Type, IState<T>>();
            this.owner = owner;
        }

        public void AddState(IState<T> state)
        {
            states[state.GetType()] = state;
        }

        public void SetInitialState<TState>() where TState : IState<T>
        {
            Type stateType = typeof(TState);
            if (states.ContainsKey(stateType))
            {
                currentState = states[stateType];
                currentState.Enter(owner);
            }
            
        }

        public void ChangeState<TState>() where TState : IState<T>
        {
            Type stateType = typeof(TState);
            if (states.ContainsKey(stateType))
            {
                if (currentState != null)
                {
                    currentState.Exit(owner);
                    previousState = currentState;
                }
                currentState = states[stateType];
                currentState.Enter(owner);
            }
        }

        public void RevertToPreviousState()
        {
            if (previousState != null)
            {
                if (currentState != null)
                {
                    currentState.Exit(owner);
                }

                currentState = previousState;
                currentState.Enter(owner);
            }
        }

        public void Update()
        {
            if (currentState != null)
            {
                currentState.Execute(owner);
            }
        }

        public IState<T> GetCurrentState()
        {
            return currentState;
        }

        public bool IsInState<TState>() where TState : IState<T>
        {
            return currentState?.GetType() == typeof(TState);
        }

        
    }
}