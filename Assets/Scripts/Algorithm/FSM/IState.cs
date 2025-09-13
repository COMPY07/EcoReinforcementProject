namespace FSM
{
    
    public interface IState<T> {
        
        
        void Enter(T owner); // 들어왔을 때 한번
        void Execute(T owner); // 지속 반복
        void Exit(T owner); // 나갈때 한번

    }
}