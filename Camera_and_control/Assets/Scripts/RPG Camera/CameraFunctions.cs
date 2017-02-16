using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//	vr: 0.3a00.3b00
public class CameraFunctions : MonoBehaviour {
	
	public GameObject Player_Obj;
	public GameObject X_Rote_Cent;
	public GameObject Cam_Obj;
	public float Hight_Offset = 0f;
	public int Edge_Boundary = 1;	//	valuable use for detect limit movement which mouse move near screen edge, unit in pixel 
	public float Look_Sensitivity = 50f;
	public float Mouse_Scroll_Sensitivity = 80f;
	public float Mouse_Scroll_SmoothDamp = 0.08f;
	public float Look_SmoothDamp = 0.05f;	//	This value lager makes smooth slower
	[HideInInspector]
	public float Max_Field_View = 80f;	//	not inuse
	[HideInInspector]
	public float Min_Field_View = 18f;	//	not inuse
	[HideInInspector]
	public float Cam_Field_View_Sensitivity = 2f;	//	not inuse
	public float Max_Cam_Distance = 6f;
	public float RTS_Fir_Cam_Distance = 10f;
	public float RTS_Sec_Cam_Distance = 15f; 
	public float Distance_Change_Sensitivity = 2f;
	public float Distance_Change_SmoothDamp = 0.05f;
	public float Angle_Change_Sensitivity = 2f;
	public float Min_Cam_Distance = 0.8f;
	public float Cam_Independent_Distance_Value = 6f;	//	Distance judgement value for cam to free it self
	public float Max_X_Rotation_Angle = 80f;
	public float Min_X_Rotation_Angle = 0f;
	public float RPG_Min_X_Rotation_Angle = -80f;
	public bool RTS_Plan_View_Flag = false;	//	A flag to determine if use plan view when changing field view value 
	public bool Move_Debug = false;
	[HideInInspector]
	public bool followPlayerFlag = true;	//	A bool flag for Player_Camera_Controller_RTS_RPG script use to control camera follow or not follow player 

	private float xMaxRote = 0f;
	private float xMinRote = 0f;
	private float camMaxDis = 0f;
	private float camMinDis = 0f;
	private float xRotation = 0f;
	private float yRotation = 0f;

	private float smoXRotCach = 0f;	//	smooth Cache and offset. Math.Smooth result store valuable,  
	private float smoYRotOff = 0f;	//	if claim in function block will cause incorrect result
	private float smoYFolOff = 0f;	//	which present as camera view vibration violently
	private float smoDisCach = 0f;	//	smooth Distance Cache
	private float smoMousCach = 0f;	//	smooth Mouse Cache
	private float smoMousTarg = 0f;	//	smooth Mouse Target

	private Transform XRoteCent;
	private Camera PlayerCam;
	private float Cam_Rotate_Distance_Factor;
	private float autoDisSave;
	private float autoAngSave;
	private bool planRTS = false;
	private float oldDiff = 0f;
	private float oldAng = 0f;

	private int stateRTSFPS = 0;
	private int stateCamAutoDis = 0;
	private int stateCamRTS = 0;
	//	private int stateCamFPS = 0;	//not yet inuse

	// Use this for initialization
	void Start () {
		PlayerCam = Cam_Obj.GetComponent<Camera> ();
		XRoteCent = X_Rote_Cent.GetComponent<Transform> ();
		Cam_Rotate_Distance_Factor = (Max_Cam_Distance - Min_Cam_Distance) / Max_X_Rotation_Angle;
		xRotation = transform.rotation.eulerAngles.x;	//	give initial angle keep same in sence
		xMaxRote = Max_X_Rotation_Angle;
		xMinRote = Min_X_Rotation_Angle;
		camMaxDis = Max_Cam_Distance;
		camMinDis = Min_Cam_Distance;

		//camINITIALIZE ();
	}
	
	// Update is called once after every frame updated
	void LateUpdate () {

		DEBUG ();	//	call debug functions

		//	get physics input
		if (Input.GetMouseButton (2)) {	//	if mid mouse button has been pushed down
			xRotation += Input.GetAxis ("Mouse Y") * Look_Sensitivity * Time.deltaTime;	//	record mouse movement on Y axis, change obj X angle in world axis
			yRotation += Input.GetAxis ("Mouse X") * Look_Sensitivity * Time.deltaTime;	//	record mouse movement on X axis, change obj Y angle in world axis
		}

		float rawMousMidTrc = Input.GetAxisRaw ("Mouse ScrollWheel");	//	record mid mouse scroll wheel movement

		float smoMousMidTrc = SMO_MousTrack (rawMousMidTrc);	//	smooth mouse scroll wheel value

		if (followPlayerFlag) {	//	Only execuate in RPG mode, means camera follow player
			float xRotOff, yRotOff;
			Edge_Move_Control(out xRotOff, out yRotOff);	//	call block to detect mouse position and rotate camera center
			xRotation += xRotOff;
			yRotation += yRotOff;
		}

		/*	---	Not yet start!!!
		 *	Try to access Player_Camera_Controller_RTS_RPG script
		 *	if the script is assigned then function of:
		 *		when field of view larger then certain point
		 *		then camera automaticlly disconnect from player 
		 */
		if (Player_Obj.GetComponent ("Player_Camera_Controller_RTS_RPG") != null){
			if (planRTS) {
				followPlayerFlag = false;
			}
		}

		//	call function block for movement
		if (followPlayerFlag) {
			FOLLOW (Hight_Offset);
		} else {
			INDEPENDENT ();
		}

		/* 
		 * detect midmouse movement
		 * call function block to move camera far and near
		 * change rotation at same time
		 * use Cam_Rotate_Distance_Factor to sync center rotate angle and camera move distance
		 * if cam default distance is 1.5f, max distance is 10f then move distance is 8.5f
		 * max move angle is 60f then Cam_Rotate_Distance_Factor = 8.5f / 60f
		 */
		CAM_DIS_MANAGER (rawMousMidTrc, smoMousMidTrc, Mouse_Scroll_Sensitivity);

		if (!followPlayerFlag) {
			//AutoHight ();	//	auto change hight when camera mode get into RTS
		}
		/*
		 * Mathf.Clamp (valuable, min, max);
		 * use to limit value between MAX and MIN
		 */
		xRotation = Mathf.Clamp (xRotation, xMinRote, xMaxRote);
		ROTATE (xRotation, yRotation);//	call function block for rotation
	}

	/********************************
	 * --- Functions
	 ********************************/

	//	get current mouse position on screen
	private void MousPos (out float mousXPos, out float mousYPos) {
		//	current mouse position on screen in pixels
		//	0 point is at left, down of game window
		mousXPos = Input.mousePosition.x;	
		mousYPos = Input.mousePosition.y;
	}

	//	Edge Boundary Movement Control
	//	out xRotOff: x Rotation Offset, out yRotOff: y Rotation Offset
	private void Edge_Move_Control (out float xRotOff, out float yRotOff) {
		float mousXPos, mousYPos;
		float newXOff = 0f, newYOff = 0f;
		float curScreWid = Screen.width;	//	read current game window width in pixels
		float curScreHei = Screen.height;	//	read current game window height in pixels

		MousPos (out mousXPos, out mousYPos);

		//	mouse movement on X axis, change obj Y angle in world axis
		if (mousXPos > curScreWid - Edge_Boundary) {
			newYOff = Look_Sensitivity * Time.deltaTime;
		} else if (mousXPos < 0 + Edge_Boundary) {
			newYOff = -Look_Sensitivity * Time.deltaTime;
		}

		//	mouse movement on Y axis, change obj X angle in world axis
		if (mousYPos > curScreHei - Edge_Boundary) {
			newXOff = Look_Sensitivity * Time.deltaTime;
		} else if (mousYPos < 0 + Edge_Boundary) {
			newXOff = -Look_Sensitivity * Time.deltaTime;
		}

		xRotOff = newXOff;
		yRotOff = newYOff;
	}

	//	movement when follow player
	//	HF: Hight Offset
	private void FOLLOW (float HF){
		
		Vector3 tempPos = new Vector3 (Player_Obj.transform.position.x, Player_Obj.transform.position.y + HF, Player_Obj.transform.position.z);
		transform.position = tempPos;
	}

	//	movement when not follow player
	private void INDEPENDENT (){
		
		Vector3 temprPosOffset = new Vector3 (0f,0f,0f);
		temprPosOffset = transform.position - Player_Obj.transform.position;
		transform.position = Player_Obj.transform.position + temprPosOffset; //may not correct
	}

	//	rotate center point angle
	//	xR: Mouse xRotation, yR: Mouse yRotation
	private void ROTATE (float xR, float yR) {

		//	must use Mathf.SmoothDampAngle otherwise camera will spine 360 when rotate player angle pass 0
		SMO_ANG(smoXRotCach, xR, Look_SmoothDamp, out smoXRotCach);
		SMO_ANG(smoYRotOff, yR, Look_SmoothDamp, out smoYRotOff);
		SMO_ANG(smoYFolOff, Player_Obj.transform.eulerAngles.y, Look_SmoothDamp, out smoYFolOff);
		float yAng = smoYRotOff + smoYFolOff;
		transform.rotation = Quaternion.Euler (0f, yAng, 0f);
		XRoteCent.rotation = Quaternion.Euler (smoXRotCach, yAng, 0f);
	}

	//	Change Fieldview
	//	mSV: mouse Track Value, CFVS: Cam Field View Sensitivity
	private void CF(float mTV, float CFVS){
		float maxFV = Max_Field_View;
		float minFV = Min_Field_View;
		float curFV = PlayerCam.fieldOfView;
		float newFV = 0f;

		if (mTV > 0f) {
			if (curFV > minFV) {
				newFV = curFV - CFVS;
			}
		} else if (mTV < 0f) {
			if (curFV < maxFV) {
				newFV = curFV + CFVS;
			}
		}

		//	if field view allow to be change, newFV remain 0f only if change value out of max and min range
		if (newFV != 0f) {
			PlayerCam.fieldOfView = newFV;
		}
	}

	//	Camera Distance Management
	//	rMTV: raw Mouse Track Value, sMTV: smooth Mouse Track Value, sens: cam distance change Sensitivity
	//	out rotateX: rotation X
	private void CAM_DIS_MANAGER (float rMTV, float sMTV, float sens) {
		float curDis = Vector3.Distance (transform.position, PlayerCam.transform.position);
		float newDisOffset = 0f;
		float mouseTV = 0f;	//	mouse mid track value apply with time.deltatime

		/*
		 * (sens * MTV * Time.deltaTime) is offset for new distance
		 * and need to multiply with -1 in order to use on real new distance calculation
		 * this valuable is use to calculate how far away for camera to move with every mid-mouse scroll scale
		 * 
		 * --- not finished yet
		 * this may not best solution, should add a function to smooth mid button track value
		 */
		if (sMTV != 0f) {
			mouseTV = sens * sMTV * Time.deltaTime;
			//	rotate center point angle when changing distance
			//if (xRotation <= xMaxRote) {
			//	xRotation -= (MTV * Look_Sensitivity * Time.deltaTime) / Cam_Rotate_Distance_Factor;
			//}
		}

		switch (stateRTSFPS) {
		case 0:
			if (curDis < Min_Cam_Distance + 0.01f & rMTV > 0f) {	//	switch to FPS view
				//stateRTSFPS = 0;
				//newDisOffset = FPSDaA (MTV, curDis);
			} else if (curDis > Max_Cam_Distance - 0.001f & rMTV < 0f & RTS_Plan_View_Flag) {	//	Switch to RTS View, check rMTV is to reduce sensivity of RPG camera and RTS camera changement
				oldAng = xRotation;
				stateCamRTS = 0;
				planRTS = true;	//	disconnect with player, cancle cam distance limitation
				stateRTSFPS = 10;
			} else {
				float autoOffset = ACCD (mouseTV, curDis);	//	auto change cam pos if cam view has been block
				if (autoOffset == 0f) {
					newDisOffset = autoOffset - mouseTV;	//	mouse track only has effect when auto change function allow
				} else {
					newDisOffset = autoOffset;
				}
			}
			break;
		case 10:
			int curState;
			RTSDaA (rMTV ,curDis,
				out newDisOffset, out curState);

			if (curState < 30) {
				xRotation = xMaxRote;
			} else if (curState == 30) {
				xRotation = oldAng;
			}

			if (rMTV > 0 & curDis < camMaxDis + 0.1f & curState == 30) {	//	if mid mouse track keep forwad and view almost return
				planRTS = false;	//	disconnect with player, cancle cam distance limitation
				stateRTSFPS = 0;
			} else if (curDis <= camMaxDis + 0.01f & curState == 30) {	//	if view almost return
				planRTS = false;	//	disconnect with player, cancle cam distance limitation
				stateRTSFPS = 0;
			}
			break;
		}

		// range limitation
		float newDis = curDis + newDisOffset;	//	calculate new position should be after apply offset
		if (newDis < camMinDis & !planRTS) {
			newDisOffset = -(curDis - camMinDis);
		} else if (newDis > camMaxDis & !planRTS) {
			newDisOffset = (camMaxDis - curDis);
		} 

		CDFRoPtC (newDisOffset, curDis);	//	call block to change the distance
	}

	//	FPS	Distance and Angle
	//	curDis: current Distance, mouseTV: mouse Track Value apply with time.deltatime
	private float FPSDaA (float mTV, float curDis) {
		float aa = 0f;
		return aa;
	}
	//	RTS Distance and Angle
	//	rMTV: raw Mouse Track Value, curDis: current Distance, mouseTV: mouse Track Value apply with time.deltatime
	//	out newPosOffset: new Position Offset, out curState: current State (stateCamRTS)
	private void RTSDaA (float rMTV,float curDis, out float newPosOffset, out int curState) {

		switch (stateCamRTS) {
		case 0:	//	go to first distance state
			oldDiff = RTS_Fir_Cam_Distance - curDis;
			smoDisCach = 0f;
			stateCamRTS = 10;
			break;
		case 10:	//	in first distance
			if (rMTV < 0 & curDis > RTS_Fir_Cam_Distance - 1f & curDis < RTS_Fir_Cam_Distance + 1f) {	//	mouse track backward, check curDis and rMTV is to reduce mid mouse truck sensitivity
				oldDiff = RTS_Sec_Cam_Distance - curDis;
				smoDisCach = 0f;
				stateCamRTS = 20;
			} else if (rMTV > 0 & curDis > RTS_Fir_Cam_Distance - 1f & curDis < RTS_Fir_Cam_Distance + 1f) {	//	mouse track forward
				oldDiff = camMaxDis - curDis;
				smoDisCach = 0f;
				stateCamRTS = 30;
			}
			break;
		case 20:	//	in second distance
			if (rMTV > 0 & curDis > RTS_Sec_Cam_Distance - 1f) {	//	mouse track forward
				oldDiff = RTS_Fir_Cam_Distance - curDis;
				smoDisCach = 0f;
				stateCamRTS = 10;
			}
			break;
		case 30:	//	quitting
			if (rMTV < 0 & curDis < camMaxDis + 1f) {	//	mouse track backward
				stateCamRTS = 0;
			}
			break;
		} 
		SMO_VAL_OFFSET (smoDisCach, oldDiff, Distance_Change_SmoothDamp * 2,
			out smoDisCach, out newPosOffset);	//	smooth value and get out put
		curState = stateCamRTS;
	}

	// Auto change Hight if somthing block between camera to center point
	private void AutoHight () {
		float curDis = 0f;

		RaycastHit rayHit;
		if (Physics.Raycast (transform.position, -transform.up, out rayHit, 100)) {
			curDis = Vector3.Distance (transform.position, rayHit.point);
		}
		//Debug.Log ("curDis?: " + curDis);
		if (curDis != 30f) {
			float diff = 30f - curDis;
			transform.position = Vector3.Lerp (transform.position, transform.position + new Vector3 (0f, diff, 0f), Time.deltaTime);
		}
	}

	//	Ray From Camera to Center
	private void RayCamCent (float curDis, out float rayDisOut) {
		float newRayDis = Mathf.Infinity;

		//	Method from Unity Manual "Direction and Distance from One Object to Another"
		Vector3 camHeading = transform.position - PlayerCam.transform.position;
		//float tempDis = tempHeading.magnitude;	//	use to calculation distance, same result as Vector3.Distance
		Vector3 camDir = camHeading / curDis;

		float maxDis = camMaxDis;
		if (camMaxDis < curDis) {
			maxDis = curDis;
		}
		RaycastHit objHit;	//	object detect ray between player and max distance
		if (Physics.Raycast (PlayerCam.transform.position, camDir, out objHit, maxDis)) {
			newRayDis = objHit.distance;
		}
		rayDisOut = newRayDis;
	}

	//	Ray From Center to Camera
	private void RayCentCam (float curDis, out float rayDisOut) {
		float newRayDis = Mathf.Infinity;

		//	Method from Unity Manual "Direction and Distance from One Object to Another"
		Vector3 camHeading = PlayerCam.transform.position - transform.position;
		//float tempDis = tempHeading.magnitude;	//	use to calculation distance, same result as Vector3.Distance
		Vector3 camDir = camHeading / curDis;

		float maxDis = camMaxDis;
		if (camMaxDis < curDis) {
			maxDis = curDis;
		}
		RaycastHit objHit;	//	object detect ray between player and max distance
		if (Physics.Raycast (transform.position, camDir, out objHit, maxDis)) {
			newRayDis = objHit.distance;
		}
		rayDisOut = newRayDis;
	}

	//	Auto Change Camera Distance 
	//	mouseTV: mouse Track Value apply with time.deltatime, curDis: current Distance
	private float ACCD (float mouseTV, float curDis){
		//float rayDis = Mathf.Infinity;
		float rayDis;
		float smoDisOff = 0f;	//	smooth distance offset
		float newDiff = 0f;

		/*
		//	Method from Unity Manual "Direction and Distance from One Object to Another"
		Vector3 camHeading = PlayerCam.transform.position - transform.position;
		//float tempDis = tempHeading.magnitude;	//	use to calculation distance, same result as Vector3.Distance
		Vector3 camDir = camHeading / curDis;
		RaycastHit objHit;	//	object detect ray between player and max distance
		if (Physics.Raycast (transform.position, camDir, out objHit, camMaxDis)) {
			rayDis = objHit.distance;
		}
		*/
		RayCentCam (curDis, out rayDis);

		/*
		 * ---	not finished yet
		 * if mouse scroll then quit auto adjust distance
		 * this function may not the best solution
		 * another way is to change final target, function only quit if finial target is less than current distance
		 */
		if (mouseTV > 0f & stateCamAutoDis == 10) {	//	only quit in forward movement when there is block behind cam, this prevent bad cam movement
			smoDisCach = 0f;
			oldDiff = 0f;
			stateCamAutoDis = 0;
		} else if (mouseTV != 0f & stateCamAutoDis > 10) {
			smoDisCach = 0f;
			oldDiff = 0f;
			stateCamAutoDis = 0;
		}

		/*
		 * get difference between camera distance and ray collide
		 * multiply with minus 1 makes camera movement offset calculation easier
		 */
		float diff = -(curDis - rayDis);	//	target difference offset value

		switch (stateCamAutoDis) {
		case 0:
			if (diff < 0) {	//	if there is something block between camera and player
				autoDisSave = curDis;	//	save current position as calculation original point
				//AutoAngSave = xRotation;
				oldDiff = diff;	//	save different distance as smooth target
				oldDiff = 0f;	//	initialize cache
				smoDisCach = 0f;	//	initialize cache
				stateCamAutoDis = 10;
			}
			break;
		case 10:
			newDiff = -(autoDisSave - rayDis);	//	calcuate for new difference base on original point
			if (curDis + newDiff < autoDisSave) {
				oldDiff = newDiff;	//	if different distance changes lower than original point then save as new target
			} else {
				oldDiff = autoDisSave - curDis;	//	if different distance larger than original point then target original point
				smoDisCach = 0f;	//	clear calculation cache, otherwise result will be greater than expect in other state
				stateCamAutoDis = 20;
			}

			SMO_VAL_OFFSET (smoDisCach, oldDiff, Distance_Change_SmoothDamp,
				out smoDisCach, out smoDisOff);	//	smooth value and get out put
			
			break;
		case 20:
			newDiff = -(autoDisSave - rayDis);
			if (curDis + newDiff < autoDisSave) {
				oldDiff = newDiff;
				smoDisCach = 0f;	//	clear calculation cache, otherwise result will be greater than expect in other state
				stateCamAutoDis = 10;
			}

			SMO_VAL_OFFSET (smoDisCach, oldDiff, Distance_Change_SmoothDamp,
				out smoDisCach, out smoDisOff);	//	smooth value and get out put
			
			if (smoDisCach == oldDiff) {	//	cam move back to original point, claer all smooth calculation cache 
				smoDisCach = 0f;
				oldDiff = 0f;
				stateCamAutoDis = 0;
			}

			break;
		}

		return smoDisOff;
	}

	//	Change Distance From Ray of Player to Camera
	//	disPos: distance Position, curDis:	current Distance
	private void CDFRoPtC (float disOffset, float curDis) {
		
		//	Method from Unity Manual "Direction and Distance from One Object to Another"
		Vector3 camHeading = PlayerCam.transform.position - transform.position;
		Vector3 camDir = camHeading / curDis;
		Ray camRay = new Ray (transform.position, camDir);
		PlayerCam.transform.Translate (camRay.direction * disOffset, Space.World);
	}

	//	Camera Look at Center Point
	private void CLaCP (){
		
		//	Rotate Camera look at center point
		Vector3 tempRote = transform.position - PlayerCam.transform.position;
		Quaternion tempQuat = Quaternion.LookRotation (tempRote);
		PlayerCam.transform.rotation = tempQuat;
	}

	//	Initialize camera view and center angle
	private void camINITIALIZE (){
		
		//CLaCP ();	//	Camera Look at Center Point
		Vector3 tempPut = new Vector3 (0f, 0f, -camMinDis);
		PlayerCam.transform.position = tempPut;	//	Move camera to minment distance make good for rest calculation 
	}

	// Smooth Angle transform function
	private void SMO_ANG (float curAng, float tarAng, float smoTim, out float result) {
		float smoothV = 0f;	//calculation temp cache for Mathf.SmoothDamp

		result = Mathf.SmoothDampAngle (curAng, tarAng, ref smoothV, smoTim);
	}

	// Smooth Value transform function
	private void SMO_VAL (float curVal, float tarVal, float smoTim, out float result) {
		float smoothV = 0f;	//calculation temp cache for Mathf.SmoothDamp

		result = Mathf.SmoothDamp (curVal, tarVal, ref smoothV, smoTim);
	}
		
	//	Smooth Value and give Offset
	//	cache: cache (current Value), target: target value, smoothDamp: smoothDamp
	//	out cacheOut: cache value output, out offset: offset result after calculation
	private void SMO_VAL_OFFSET (float cache, float target, float smoothDamp, out float cacheOut, out float offset) {
		float oldDisOff = cache;	//	save old movement calculation cache value

		SMO_VAL (cache, target, smoothDamp, out cacheOut);	//	smooth movemeant
		offset = (cacheOut - oldDisOff);	//	calculate offset value
	}

	//	Smooth mid Mouse Track value
	private float SMO_MousTrack (float mouseTrackInput){
		float oldDisOff = smoMousCach;

		smoMousTarg += mouseTrackInput;
		SMO_VAL (smoMousCach, smoMousTarg, Mouse_Scroll_SmoothDamp, out smoMousCach);
		float smoothOutPut = smoMousCach - oldDisOff;

		if (smoMousCach == smoMousTarg) {
			smoMousCach = smoMousTarg = 0f;	//	clear 0, keep cache clean
		}

		return smoothOutPut;
	}

	//	debug functions, test camera movement by keyboard
	private void DEBUG (){
		
		//	Use keyboard to debug if mouse function is not working well
		if (Move_Debug) {
			if (Input.GetKey (KeyCode.O)) {
				xRotation += 1f * Look_Sensitivity * Time.deltaTime;
			} else if(Input.GetKey (KeyCode.P)) {
				xRotation -= 1f * Look_Sensitivity * Time.deltaTime;
			}
			if (Input.GetKey (KeyCode.K)) {
				yRotation += 1f * Look_Sensitivity * Time.deltaTime;
			} else if(Input.GetKey (KeyCode.L)) {
				yRotation -= 1f * Look_Sensitivity * Time.deltaTime;
			}
		}
	}
}
//Debug.Log (" rayDis?: " + rayDis);