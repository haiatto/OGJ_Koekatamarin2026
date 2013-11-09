using UnityEngine;
using System.Collections;

public class TextManage : MonoBehaviour {
	
	public TextMesh []Text;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
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
}
