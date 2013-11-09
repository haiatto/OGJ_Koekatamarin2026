using UnityEngine;
using System.Collections;

public class shoot : MonoBehaviour {
	
	public GameObject BlockPrefab;
	public int ShootPow=10;
		
	Quaternion OVR_angle;
	GameObject PlayerObj;
	int a=0;
	// Use this for initialization
	void Start () {
		PlayerObj=GameObject.Find("Player");
		OVR_angle = Quaternion.identity;
	}
	
	// Update is called once per frame
	void Update () {
//		if(Input.GetKey(KeyCode.B)){
		if (a%60==0){
 			Shoot("a");}
		a++;
			
		//}
	
	}
	void Shoot(string mes){

		OVRDevice.GetPredictedOrientation(0, ref OVR_angle);
		GameObject createBlock	= (GameObject)Instantiate(this.BlockPrefab, PlayerObj.transform.position, OVR_angle);
		
		var shootWorldQuat = PlayerObj.transform.rotation;
		
		createBlock.SendMessage("ChangeText",mes);
		if(Random.Range(0,3)==0){
			createBlock.SendMessage("ChangeColor",new Color(1.0f,0.0f,0.0f));
		}
		
		createBlock.rigidbody.velocity=shootWorldQuat*new Vector3(0,5,ShootPow);
//		this.gameObject.rigidbody.velocity=new Vector3(ShootPow,0,0);
		
		
	}
}
