using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneRL
{
    public sealed class RescueCamera : MonoBehaviour
    {
        public enum View { Isometric, Top, Follow, Orbit }
        public View Mode { get; private set; }=View.Isometric;
        public Transform Drone;
        public Func<Vector2,bool> PointerOverUI;
        public Action OcclusionUpdate;
        Vector3 focus=new Vector3(0,0.5f,0);
        float yaw=-35,pitch=52,distance=48;
        void Start()
        {
            var rotation=Quaternion.Euler(pitch,yaw,0);
            transform.SetPositionAndRotation(focus+rotation*Vector3.back*distance,rotation);
        }
        public void SetView(View mode)
        {
            Mode=mode;
            if(mode==View.Isometric){focus=new Vector3(0,0.5f,0);yaw=-35;pitch=52;distance=48;}
            if(mode==View.Top){focus=Vector3.zero;yaw=0;pitch=88;distance=44;}
            if(mode==View.Follow){yaw=-25;pitch=38;distance=13;}
        }
        void LateUpdate()
        {
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            if(keyboard!=null&&Application.isFocused)
            {
                if(keyboard.digit1Key.wasPressedThisFrame)SetView(View.Isometric);
                if(keyboard.digit2Key.wasPressedThisFrame)SetView(View.Top);
                if(keyboard.digit3Key.wasPressedThisFrame)SetView(View.Follow);
                if(keyboard.rKey.wasPressedThisFrame)SetView(View.Isometric);
            }
            if(Mode==View.Follow&&Drone!=null)focus=Vector3.Lerp(focus,Drone.position,1-Mathf.Exp(-6*Time.unscaledDeltaTime));
            if(mouse!=null&&Application.isFocused&&(PointerOverUI==null||!PointerOverUI(mouse.position.ReadValue())))
            {
                var delta=mouse.delta.ReadValue();
                if(mouse.rightButton.isPressed&&delta.sqrMagnitude>0.001f)
                {
                    Mode=View.Orbit;
                    yaw+=delta.x*0.18f;pitch=Mathf.Clamp(pitch-delta.y*0.18f,22,85);
                }
                if(mouse.middleButton.isPressed)
                {
                    Mode=View.Orbit;
                    var right=Quaternion.Euler(0,yaw,0)*Vector3.right;
                    var forward=Quaternion.Euler(0,yaw,0)*Vector3.forward;
                    focus-=right*delta.x*distance*0.0012f+forward*delta.y*distance*0.0012f;
                    focus.x=Mathf.Clamp(focus.x,-13,13);focus.z=Mathf.Clamp(focus.z,-13,13);
                }
                distance=Mathf.Clamp(distance-mouse.scroll.ReadValue().y*0.012f,8,58);
            }
            var rotation=Quaternion.Euler(pitch,yaw,0);
            var position=focus+rotation*Vector3.back*distance;
            position.y=Mathf.Max(position.y,3.5f);
            float t=1-Mathf.Exp(-10*Time.unscaledDeltaTime);
            transform.position=Vector3.Lerp(transform.position,position,t);
            transform.rotation=Quaternion.Slerp(transform.rotation,rotation,t);
            OcclusionUpdate?.Invoke();
        }
    }
}
