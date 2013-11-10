using System.Collections.Generic;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PhotonView))]
public class multiShoot : Photon.MonoBehaviour {	
	public GameObject BlockPrefab;
	public int ShootPow=10;
	
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}
    [RPC]
    public void ShootMulti(string mes, Vector3 shotPos,Quaternion shotDir, PhotonMessageInfo mi)
    {
        string senderName = "anonymous";

        if (mi != null && mi.sender != null)
        {
            if (!string.IsNullOrEmpty(mi.sender.name))
            {
                senderName = mi.sender.name;
            }
            else
            {
                senderName = "player " + mi.sender.ID;
            }
        }

		//OVRDevice.GetPredictedOrientation(0, ref OVR_angle);
		GameObject createBlock	= Instantiate(this.BlockPrefab, shotPos, shotDir) as GameObject;
		TextManage textManage = createBlock.GetComponent<TextManage>();
		
		var shootWorldQuat = shotDir;//PlayerObj.transform.rotation;
		
		//textManage.SendMessage(mes);
		createBlock.SendMessage("ChangeText",mes);
		if(Random.Range(0,3)==0){
			createBlock.SendMessage("ChangeColor",new Color(1.0f,0.0f,0.0f));
		}
		
		createBlock.rigidbody.velocity=shootWorldQuat*new Vector3(0,5,ShootPow);
		
		//if(null!=applyFunc)
		//{
		//	applyFunc(createBlock);
		//}
		
//		this.gameObject.rigidbody.velocity=new Vector3(ShootPow,0,0);

    }

    public void MultiShootTes(string mes, Vector3 shotPos,Quaternion shotDir)
    {
		//OVRDevice.GetPredictedOrientation(0, ref OVR_angle);
		GameObject createBlock	= Instantiate(this.BlockPrefab, shotPos, shotDir) as GameObject;
		TextManage textManage = createBlock.GetComponent<TextManage>();
		
		var shootWorldQuat = shotDir;//PlayerObj.transform.rotation;
		
		textManage.SendMessage(mes);
		createBlock.SendMessage("ChangeText",mes);
		if(Random.Range(0,3)==0){
			createBlock.SendMessage("ChangeColor",new Color(1.0f,0.0f,0.0f));
		}
		
		createBlock.rigidbody.velocity=shootWorldQuat*new Vector3(0,5,ShootPow);
    }
}
