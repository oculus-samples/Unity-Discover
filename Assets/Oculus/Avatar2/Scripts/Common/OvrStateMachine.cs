using System;
using System.Collections.Generic;

namespace Oculus.Avatar2
{
    public class OvrStateMachine<T> where T : System.Enum
    {
        public T currentState;

        public delegate bool CanEnterDelegate(T nextState);
        public delegate void StateChangedDelegate(T nextState, T prevState);
        public CanEnterDelegate canEnter;
        public StateChangedDelegate onStateChange;
        public bool SetState(T nextState)
        {
            if(canEnter != null && !canEnter(nextState))
            {
                return false;
            }
            T prevState = currentState;
            currentState = nextState;
            if(onStateChange != null)
            {
                onStateChange(currentState, prevState);
            }
            return true;
        }


        public bool IsState(T checkState)
        {
            return Compare(currentState, checkState);
        }

        public bool Compare(T x, T y)
        {
            return EqualityComparer<T>.Default.Equals(x, y);
        }

        public string GetStateString()
        {
            return currentState.ToString();
        }
    }
}
