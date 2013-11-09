using UnityEngine;
using System.Collections;

public class TextManage : MonoBehaviour {
	
	public float DestroyTime=5.0f;
	public TextMesh []Text;
	void Start () {
		DestroySelf(DestroyTime);
	}
	
	// Update is called once per frame
	void Update () {
	}
	void ChangeText(string mes){
		int loopCounter;
		for(loopCounter=0;loopCounter<Text.Length;loopCounter++){
			Text[loopCounter].text=mes;
		}
	}
	public void ChangeColor(Color col){
		int loopCounter;
		for(loopCounter=0;loopCounter<Text.Length;loopCounter++){
			Text[loopCounter].color=col;
		}		
	}
	public void ChangeColor(Color col,Color col2){
		int loopCounter;
		for(loopCounter=0;loopCounter<Text.Length;loopCounter++){
			Text[loopCounter].color=col;
		}
		Text[((int)Text.Length/2)].color=col2;
	}
	void DestroySelf(float timer){
		int loopCounter;
		for(loopCounter=0;loopCounter<Text.Length;loopCounter++){
			Destroy(Text[loopCounter],timer);
		}
		Destroy(this,timer);
	}

}
