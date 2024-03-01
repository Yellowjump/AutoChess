using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum StateType
{
    Idle,Attack,Move,UnderControl
}
public class FSM : MonoBehaviour
{
    private IState currentState;
    private Dictionary<StateType, IState> states = new Dictionary<StateType, IState>();

    void Start()
    {
        states.Add(StateType.Idle,new IdleState(this));
        states.Add(StateType.Attack, new AttackState(this));
        states.Add(StateType.Move, new MoveState(this));
        states.Add(StateType.UnderControl, new UnderControlState(this));
        TransitionState(StateType.Idle);
    }

    // Update is called once per frame
    void Update()
    {
        currentState.OnUpdate();
    }
    public void TransitionState(StateType type)
    {
        if (currentState!=null)
        {
            currentState.OnExit();
        }
        currentState = states[type];
        currentState.OnEnter();
    }
}
