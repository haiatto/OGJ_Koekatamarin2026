using UnityEngine;
using System.Collections;

public class TextManage : MonoBehaviour {
	
	public float DestroyTime=5.0f;
	public TextMesh []Text;
//	private float Timer;
	// Use this for initialization
	void Start () {
//		Timer=Time.time+DestroyTime;
		Destroy(this, DestroyTime);
	}
	
	// Update is called once per frame
	void Update () {
//		DestroySelf();
		
	}
	void ChangeText(string mes){
		int loopCounter;
		for(loopCounter=0;loopCounter<Text.Length;loopCounter++){
			Text[loopCounter].text=mes;
		}
	//	GameObject childObject = gameObject.transform.FindChild("Text000").gameObject;
		
	}
	void ChangeColor(Color col){
		int loopCounter;
		for(loopCounter=0;loopCounter<Text.Length;loopCounter++){
			Text[loopCounter].color=col;
		}
		
	}
/*	void DestroySelf(){
		if(Timer<Time.time){
			Destroy(this);
		}
		
	}
*/
}
