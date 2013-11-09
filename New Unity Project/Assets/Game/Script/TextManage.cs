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
	void ChangeColor(Color col){
		int loopCounter;
		for(loopCounter=0;loopCounter<Text.Length;loopCounter++){
			Text[loopCounter].color=col;
		}		
	}
	void DestroySelf(float timer){
		int loopCounter;
		for(loopCounter=0;loopCounter<Text.Length;loopCounter++){
			Destroy(Text[loopCounter],timer);
		}
		Destroy(this,timer);
	}

}
