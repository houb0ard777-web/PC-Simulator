using UnityEngine;
namespace PCSim
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TouchWalker : MonoBehaviour
    {
        public bool MenuOpen;
        public Camera View;
        CharacterController body;
        Vector2 move;
        float pitch, vertical;
        int moveFinger=-1, lookFinger=-1;
        Vector2 moveOrigin;
        void Awake() { body=GetComponent<CharacterController>(); body.height=1.75f; body.radius=.27f; body.center=new Vector3(0,.875f,0); }
        void Update()
        {
            if(MenuOpen) { move=Vector2.zero; moveFinger=lookFinger=-1; return; }
            if(Input.GetKeyDown(KeyCode.Tab)) Cursor.lockState=Cursor.lockState==CursorLockMode.Locked?CursorLockMode.None:CursorLockMode.Locked;
            move=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
            if(Cursor.lockState==CursorLockMode.Locked)
                Look(new Vector2(Input.GetAxis("Mouse X")*2,Input.GetAxis("Mouse Y")*2));
            foreach(var t in Input.touches)
            {
                if(t.phase==TouchPhase.Began)
                {
                    // Top 14% is reserved for the menu HUD; right bottom corner for Use.
                    if(t.position.y>Screen.height*.86f) continue;
                    if(t.position.x<Screen.width*.45f&&moveFinger<0) { moveFinger=t.fingerId; moveOrigin=t.position; }
                    else if(t.position.x<Screen.width*.84f&&lookFinger<0) lookFinger=t.fingerId;
                }
                if(t.fingerId==moveFinger) move=Vector2.ClampMagnitude((t.position-moveOrigin)/(Screen.height*.13f),1);
                if(t.fingerId==lookFinger&&t.phase==TouchPhase.Moved) Look(t.deltaPosition/Screen.height*110);
                if(t.phase==TouchPhase.Ended||t.phase==TouchPhase.Canceled)
                { if(t.fingerId==moveFinger) { moveFinger=-1; move=Vector2.zero; } if(t.fingerId==lookFinger) lookFinger=-1; }
            }
            vertical=body.isGrounded?-2:vertical-20*Time.deltaTime;
            var velocity=(transform.right*move.x+transform.forward*move.y)*3.2f;
            body.Move((velocity+Vector3.up*vertical)*Time.deltaTime);
            if(transform.position.y< -10) { body.enabled=false; transform.position=new Vector3(0,.1f,1); body.enabled=true; }
        }
        void Look(Vector2 delta)
        { transform.Rotate(0,delta.x,0); pitch=Mathf.Clamp(pitch-delta.y,-78,78); View.transform.localRotation=Quaternion.Euler(pitch,0,0); }
        void OnGUI()
        {
            if(MenuOpen||!Application.isMobilePlatform) return;
            GUI.color=new Color(1,1,1,.45f);
            var radius=Screen.height*.13f;
            GUI.Box(new Rect(Screen.width*.13f-radius,Screen.height*.72f-radius,radius*2,radius*2),"ДВИЖЕНИЕ");
            GUI.Label(new Rect(Screen.width*.55f,Screen.height*.8f,Screen.width*.27f,50),"Проведи для обзора");
            GUI.color=Color.white;
        }
    }
}
