﻿using Leap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace LeapGestureRecognition
{
	[DataContract]
	public class SGInstanceSingleHand
	{
		public SGInstanceSingleHand() { }

		// Gotta figure out how to handle both hands
		public SGInstanceSingleHand(Hand hand)
		{
			HandTransform = getHandTransform(hand);

			IsLeft = hand.IsLeft;
			IsRight = hand.IsRight;
			PalmPosition = new Vec3(hand.PalmPosition); // Do not HandTransform.TransformPoint()
			PalmNormal = new Vec3(hand.PalmNormal);
			HandDirection = new Vec3(hand.Direction);
			ArmX = new Vec3(hand.Arm.Basis.xBasis);
			ArmY = new Vec3(hand.Arm.Basis.yBasis);
			ArmZ = new Vec3(hand.Arm.Basis.zBasis);

			// Normalize these angle values. (They all range between -PI and PI).
			Yaw = hand.PalmNormal.Yaw;// / (float)Math.PI;
			Pitch = hand.PalmNormal.Pitch;// / (float)Math.PI;
			Roll = hand.PalmNormal.Roll;// / (float)Math.PI;

			// World coordinates
			WristPos_World = new Vec3(hand.WristPosition);
			ElbowPos_World = new Vec3(hand.Arm.ElbowPosition);
			ForearmCenter_World = new Vec3(hand.Arm.Center);
			setFingerJointPositions_World(hand);
			setFingerBasePositions_World(hand);

			// Relative coordinates
			WristPos_Relative = new Vec3(hand.WristPosition);
			ElbowPos_Relative = new Vec3(hand.Arm.ElbowPosition);
			ForearmCenter_Relative = new Vec3(hand.Arm.Center);
			// NOTE: HandScale and relative finger positions must be calculated after world coordinates. 
			//HandScale = GetFingerLength(Finger.FingerType.TYPE_MIDDLE);

			FingerLengths = GetFingerLengths();

			setFingerJointPositions_Relative(hand);
			setFingerBasePositions_Relative(hand);

			PalmSphereRadius = hand.SphereRadius / FingerLengths[Finger.FingerType.TYPE_MIDDLE];
			PalmSphereCenter = new Vec3(HandTransform.TransformPoint(hand.SphereCenter)) / FingerLengths[Finger.FingerType.TYPE_MIDDLE];

			buildFingerFeatures(hand);
		}

		[DataMember]
		public bool IsLeft { get; set; }
		[DataMember]
		public bool IsRight { get; set; }
		[DataMember]
		public Vec3 PalmPosition { get; set; }
		[DataMember]
		public Vec3 PalmNormal { get; set; }
		[DataMember]
		public Vec3 HandDirection { get; set; }
		[DataMember]
		public float Yaw { get; set; }
		[DataMember]
		public float Pitch { get; set; }
		[DataMember]
		public float Roll { get; set; }
		[DataMember]
		public Vec3 ArmX { get; set; }
		[DataMember]
		public Vec3 ArmY { get; set; }
		[DataMember]
		public Vec3 ArmZ { get; set; }


		// Coordinates relative to the hand's object space:
		//private float HandScale;
		[DataMember]
		public Dictionary<Finger.FingerType, Dictionary<Finger.FingerJoint, Vec3>> FingerJointPositions_Relative;
		[DataMember]
		public Vec3 WristPos_Relative { get; set; }
		[DataMember]
		public Vec3 ElbowPos_Relative { get; set; }
		[DataMember]
		public Vec3 IndexBasePos_Relative { get; set; }
		[DataMember]
		public Vec3 MiddleBasePos_Relative { get; set; }
		[DataMember]
		public Vec3 RingBasePos_Relative { get; set; }
		[DataMember]
		public Vec3 PinkyBasePos_Relative { get; set; }
		[DataMember]
		public Vec3 ForearmCenter_Relative { get; set; }

		// World coordinates: // May want to consider scaling these as well
		[DataMember]
		public Dictionary<Finger.FingerType, Dictionary<Finger.FingerJoint, Vec3>> FingerJointPositions_World;
		[DataMember]
		public Vec3 WristPos_World { get; set; }
		[DataMember]
		public Vec3 ElbowPos_World { get; set; }
		[DataMember]
		public Vec3 IndexBasePos_World { get; set; }
		[DataMember]
		public Vec3 MiddleBasePos_World { get; set; }
		[DataMember]
		public Vec3 RingBasePos_World { get; set; }
		[DataMember]
		public Vec3 PinkyBasePos_World { get; set; }
		[DataMember]
		public Vec3 ForearmCenter_World { get; set; }
		[DataMember]
		public float PalmSphereRadius { get; set; }
		[DataMember]
		public Vec3 PalmSphereCenter { get; set; }

		[DataMember]
		public Dictionary<Finger.FingerType, bool> FingersExtended { get; set; }
		[DataMember]
		public Dictionary<Finger.FingerType, Vec3> FingerTipPositions { get; set; }

		public Dictionary<Finger.FingerType, float> FingerLengths { get; set; }


		
		public Matrix HandTransform { get; set; }

		public Vec3 ThumbBasePos_Relative
		{
			// Because of 0 length metacarpal
			get { return FingerJointPositions_Relative[Finger.FingerType.TYPE_THUMB][Finger.FingerJoint.JOINT_MCP]; }
		}

		public Vec3 ThumbBasePos_World
		{
			// Because of 0 length metacarpal
			get { return FingerJointPositions_World[Finger.FingerType.TYPE_THUMB][Finger.FingerJoint.JOINT_MCP]; }
		}

		#region Private Methods
		private void setFingerJointPositions_World(Hand hand)
		{
			FingerJointPositions_World = new Dictionary<Finger.FingerType,Dictionary<Finger.FingerJoint,Vec3>>();
			foreach (var finger in hand.Fingers)
			{
				FingerJointPositions_World.Add(finger.Type, new Dictionary<Finger.FingerJoint, Vec3>());
				foreach (var jointType in (Finger.FingerJoint[])Enum.GetValues(typeof(Finger.FingerJoint)))
				{
					Vec3 worldPoint = new Vec3(finger.JointPosition(jointType));
					FingerJointPositions_World[finger.Type].Add(jointType, worldPoint);
				}
			}
		}

		private void setFingerBasePositions_World(Hand hand)
		{
			Finger index = hand.Fingers.Where(f => f.Type == Finger.FingerType.TYPE_INDEX).FirstOrDefault();
			Finger middle = hand.Fingers.Where(f => f.Type == Finger.FingerType.TYPE_MIDDLE).FirstOrDefault();
			Finger ring = hand.Fingers.Where(f => f.Type == Finger.FingerType.TYPE_RING).FirstOrDefault();
			Finger pinky = hand.Fingers.Where(f => f.Type == Finger.FingerType.TYPE_PINKY).FirstOrDefault();

			IndexBasePos_World = new Vec3(index.Bone(Bone.BoneType.TYPE_METACARPAL).PrevJoint);
			MiddleBasePos_World = new Vec3(middle.Bone(Bone.BoneType.TYPE_METACARPAL).PrevJoint);
			RingBasePos_World = new Vec3(ring.Bone(Bone.BoneType.TYPE_METACARPAL).PrevJoint);
			PinkyBasePos_World = new Vec3(pinky.Bone(Bone.BoneType.TYPE_METACARPAL).PrevJoint);
		}

		private void setFingerJointPositions_Relative(Hand hand)
		{
			FingerJointPositions_Relative = new Dictionary<Finger.FingerType, Dictionary<Finger.FingerJoint, Vec3>>();
			foreach (var finger in hand.Fingers)
			{
				FingerJointPositions_Relative.Add(finger.Type, new Dictionary<Finger.FingerJoint, Vec3>());
				foreach (var jointType in (Finger.FingerJoint[])System.Enum.GetValues(typeof(Finger.FingerJoint)))
				{
					Vec3 relativePoint = new Vec3(HandTransform.TransformPoint(finger.JointPosition(jointType))) / FingerLengths[finger.Type];
					FingerJointPositions_Relative[finger.Type].Add(jointType, relativePoint);
				}
			}
		}

		private void setFingerBasePositions_Relative(Hand hand)
		{
			Leap.Finger index = hand.Fingers.Where(f => f.Type == Finger.FingerType.TYPE_INDEX).FirstOrDefault();
			Leap.Finger middle = hand.Fingers.Where(f => f.Type == Finger.FingerType.TYPE_MIDDLE).FirstOrDefault();
			Leap.Finger ring = hand.Fingers.Where(f => f.Type == Finger.FingerType.TYPE_RING).FirstOrDefault();
			Leap.Finger pinky = hand.Fingers.Where(f => f.Type == Finger.FingerType.TYPE_PINKY).FirstOrDefault();

			IndexBasePos_Relative = new Vec3(HandTransform.TransformPoint(index.Bone(Bone.BoneType.TYPE_METACARPAL).PrevJoint)) / FingerLengths[Finger.FingerType.TYPE_INDEX];
			MiddleBasePos_Relative = new Vec3(HandTransform.TransformPoint(middle.Bone(Bone.BoneType.TYPE_METACARPAL).PrevJoint)) / FingerLengths[Finger.FingerType.TYPE_MIDDLE];
			RingBasePos_Relative = new Vec3(HandTransform.TransformPoint(ring.Bone(Bone.BoneType.TYPE_METACARPAL).PrevJoint)) / FingerLengths[Finger.FingerType.TYPE_RING];
			PinkyBasePos_Relative = new Vec3(HandTransform.TransformPoint(pinky.Bone(Bone.BoneType.TYPE_METACARPAL).PrevJoint)) / FingerLengths[Finger.FingerType.TYPE_PINKY];
		}

		private void buildFingerFeatures(Hand hand)
		{
			FingerTipPositions = new Dictionary<Finger.FingerType, Vec3>();
			FingersExtended = new Dictionary<Finger.FingerType, bool>();
			foreach (var finger in hand.Fingers)
			{
				FingerTipPositions.Add(finger.Type, FingerJointPositions_Relative[finger.Type][Finger.FingerJoint.JOINT_TIP]);
				FingersExtended.Add(finger.Type, finger.IsExtended);
			}
		}


		// Inspired by "Transforming Finger Coordinates into the Hand’s Frame of Reference"
		//	from https://developer.leapmotion.com/documentation/csharp/devguide/Leap_Hand.html
		private Matrix getHandTransform(Hand hand) // Might want to move this somewhere else
		{
			Vector yBasis = -hand.PalmNormal;
			Vector zBasis = -hand.Direction;
			Vector xBasis = yBasis.Cross(zBasis);
			Vector origin = hand.PalmPosition;
			Matrix handTransform = new Matrix(xBasis, yBasis, zBasis, origin);
			handTransform = handTransform.RigidInverse(); // I don't really understand the point of this
			return handTransform;
		}
		#endregion

		#region Public Methods
		public float GetFingerLength(Finger.FingerType fingerType)
		{
			// Finger length = dist(PalmCenter, MCP) + dist(MCP, PIP) + dist(PIP, DIP) + dist(DIP, TIP)
			var joints = FingerJointPositions_World[fingerType];
			float length = PalmPosition.DistanceTo(joints[Finger.FingerJoint.JOINT_MCP]);
			length += joints[Finger.FingerJoint.JOINT_MCP].DistanceTo(joints[Finger.FingerJoint.JOINT_PIP]);
			length += joints[Finger.FingerJoint.JOINT_PIP].DistanceTo(joints[Finger.FingerJoint.JOINT_DIP]);
			length += joints[Finger.FingerJoint.JOINT_DIP].DistanceTo(joints[Finger.FingerJoint.JOINT_TIP]);
			return length;
		}

		public Dictionary<Finger.FingerType, float> GetFingerLengths()
		{
			var fingerLengths = new Dictionary<Finger.FingerType, float>();
			foreach (var fingerType in (Finger.FingerType[])Enum.GetValues(typeof(Finger.FingerType)))
			{
				fingerLengths.Add(fingerType, GetFingerLength(fingerType));
			}
			return fingerLengths;
		}
		#endregion
	}
}
