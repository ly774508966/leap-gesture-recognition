﻿using SharpGL;
using SharpGL.SceneGraph.Quadrics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Leap;
using LeapGestureRecognition.Util;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows;
using LGR;
using System.Collections.ObjectModel;
using LeapGestureRecognition.View;
using System.Threading;
using System.Windows.Threading;
using LGR_Controls;

namespace LeapGestureRecognition.ViewModel
{
	public class FrameEventArgs : EventArgs
	{
		public Frame Frame { get; set; }
	}

	public class MainViewModel : INotifyPropertyChanged
	{
		private static OpenGL _gl;
		private static Controller _controller;
		private static System.Windows.Controls.ScrollViewer _scrollViewer;
		private static System.Windows.Controls.TextBox _outputWindowTextBox;
		private GestureLibrary _gestureLibraryControl;
		private EditStaticGesture _editStaticGestureControl;
		private EditDynamicGesture _editDynamicGestureControl;
		private RecognitionMonitor _recognitionMonitorControl;

		private static Camera _camera;

		private CustomLeapListener _listener;
		private SharpGLHelper _glHelper;
		private SQLiteProvider _sqliteProvider;
		private LGR_Configuration _config;

		private StatisticalClassifier _classifier;

		public MainViewModel(OpenGL gl, System.Windows.Controls.ScrollViewer scrollViewer, System.Windows.Controls.TextBox outputWindowTextBox, GestureLibrary gestureLibraryControl, EditStaticGesture editStaticGestureControl, EditDynamicGesture editDynamicGestureControl, RecognitionMonitor recognitionMonitorControl, Controller controller, CustomLeapListener listener)
		{
			_gl = gl;
			_controller = controller;
			_listener = listener;
			_controller.AddListener(_listener);
			_camera = new Camera(_gl);
			_scrollViewer = scrollViewer;
			_outputWindowTextBox = outputWindowTextBox;
			_gestureLibraryControl = gestureLibraryControl;
			_gestureLibraryControl.SetMvm(this);
			_editStaticGestureControl = editStaticGestureControl;
			_editStaticGestureControl.SetMvm(this);
			_editDynamicGestureControl = editDynamicGestureControl;
			_editDynamicGestureControl.SetMvm(this);
			_sqliteProvider = new SQLiteProvider(Constants.SQLiteFileName);
			_config = new LGR_Configuration(_sqliteProvider);
			_glHelper = new SharpGLHelper(_gl, _config.BoneColors);

			DynamicGestureRecorder = new DynamicGestureRecorder(this);

			UpdateStaticGestureLibrary();
			UpdateDynamicGestureLibrary();

			initMenuBar();

			// Needs to be initialized after UpdateGestureLibrary()
			_classifier = new StatisticalClassifier(StaticGestures, DynamicGestures);

			_recognitionMonitorControl = recognitionMonitorControl;
			_recognitionMonitorControl.VM = new RecognitionMonitorViewModel(_classifier);
			
			FrameReceived += _recognitionMonitorControl.VM.OnFrameReceived;
		}

		#region Events
		public event EventHandler<FrameEventArgs> FrameReceived;

		protected virtual void OnFrameReceived(Frame frame)
		{
			if (FrameReceived != null) FrameReceived(this, new FrameEventArgs() { Frame = frame });
		}
		#endregion

		#region Public Properties
		public SQLiteProvider SQLiteProvider { get { return _sqliteProvider; } }

		public double OpenGLWindowWidth { get; set; }
		public double OpenGLWindowHeight { get; set; }
		public Frame CurrentFrame { get; set; }
		public ObservableCollection<CustomMenuItem> MenuBar { get; set; }
		public DynamicGestureRecorder DynamicGestureRecorder { get; set; }

		private bool _ShowEditStaticGesture = false;
		public bool ShowEditStaticGesture 
		{ 
			get { return _ShowEditStaticGesture; }
			set
			{
				_ShowEditStaticGesture = value;
				OnPropertyChanged("ShowEditStaticGesture");
			}
		}

		private bool _ShowEditDynamicGesture = false;
		public bool ShowEditDynamicGesture
		{
			get { return _ShowEditDynamicGesture; }
			set
			{
				_ShowEditDynamicGesture = value;
				OnPropertyChanged("ShowEditDynamicGesture");
			}
		}

		private bool _ShowRecognitionMonitor = true;
		public bool ShowRecognitionMonitor 
		{
			get { return _ShowRecognitionMonitor; }
			set 
			{ 
				_ShowRecognitionMonitor = value;
				OnPropertyChanged("ShowRecognitionMonitor");
			}
		}

		private StaticGestureInstance _SelectedStaticGesture = null;
		public StaticGestureInstance SelectedStaticGesture
		{
			get { return _SelectedStaticGesture; }
			set
			{
				_SelectedStaticGesture = value;
				OnPropertyChanged("SelectedStaticGesture");
			}
		}

		private DynamicGestureInstance _SelectedDynamicGesture = null;
		public DynamicGestureInstance SelectedDynamicGesture
		{
			get { return _SelectedDynamicGesture; }
			set
			{
				_SelectedDynamicGesture = value;
				OnPropertyChanged("SelectedDynamicGesture");
			}
		}

		public static string OutputWindowContent
		{
			get { return _outputWindowTextBox.Text; }
			set
			{
				_outputWindowTextBox.Text = value;
				_scrollViewer.ScrollToBottom();
			}
		}

		//private LGR_Mode _Mode = LGR_Mode.Default;
		private LGR_Mode _Mode = LGR_Mode.Recognize;
		public LGR_Mode Mode
		{
			get { return _Mode; }
			set 
			{
				_Mode = value; 
				switch (_Mode)
				{
					case LGR_Mode.Recognize:
						ShowRecognitionMonitor = true;
						ShowEditStaticGesture = false;
						ShowEditDynamicGesture = false;
						_recognitionMonitorControl.VM = new RecognitionMonitorViewModel(_classifier);
						break;
					case LGR_Mode.EditStatic:
						ShowRecognitionMonitor = false;
						ShowEditStaticGesture = true;
						ShowEditDynamicGesture = false;
						break;
					case LGR_Mode.EditDynamic:
						ShowRecognitionMonitor = false;
						ShowEditStaticGesture = false;
						ShowEditDynamicGesture = true;
						break;
					default:
						ShowRecognitionMonitor = true;
						ShowEditStaticGesture = false;
						ShowEditDynamicGesture = false;
						break;
				}
				//OnPropertyChanged("Mode");
				OnPropertyChanged("ShowRecognitionMonitor");
				OnPropertyChanged("ShowEditStaticGesture");
				OnPropertyChanged("ShowEditDynamicGesture");
			}
		}

		public LGR_Configuration Config
		{
			get { return _config; }
			set
			{
				_config = value;
				//OnPropertyChanged("Config");
			}
		}

		private ObservableCollection<StaticGestureClassWrapper> _StaticGestures = new ObservableCollection<StaticGestureClassWrapper>();
		public ObservableCollection<StaticGestureClassWrapper> StaticGestures
		{
			get { return _StaticGestures; }
			set
			{
				lock (StaticGestures) // This was accessed by multiple threads (StatisticalClassifier has a reference to it and uses a timer).
				{
					_StaticGestures = value;
					UpdateGestureLibraryMenu(); // Should move this... will continuously clear GestureLibraryMenuItems unnecessarily 
					if (_classifier != null) _classifier.StaticGestureClasses = _StaticGestures;
					OnPropertyChanged("StaticGestures");
				}
			}
		}

		private ObservableCollection<GestureDistance> _RankedStaticGestures = new ObservableCollection<GestureDistance>();
		public ObservableCollection<GestureDistance> RankedStaticGestures
		{
			get { return _RankedStaticGestures; }
			set
			{
				_RankedStaticGestures = value;
				OnPropertyChanged("RankedStaticGestures");
			}
		}

		public void UpdateGestureLibraryMenu()
		{
			GestureLibraryMenuItems.Clear();
			foreach (var gesture in StaticGestures)
			{
				GestureLibraryMenuItems.Add(new CustomMenuItem(gesture.Name));
			}
		}

		private ObservableCollection<DynamicGestureClassWrapper> _DynamicGestures = new ObservableCollection<DynamicGestureClassWrapper>();
		public ObservableCollection<DynamicGestureClassWrapper> DynamicGestures
		{
			get { return _DynamicGestures; }
			set
			{
				lock (DynamicGestures) // This was accessed by multiple threads (StatisticalClassifier has a reference to it and uses a timer).
				{
					_DynamicGestures = value;
					//UpdateGestureLibraryMenu(); // Should move this... will continuously clear GestureLibraryMenuItems unnecessarily 
					//if (_classifier != null) _classifier.GestureClasses = _StaticGestures;
					OnPropertyChanged("DynamicGestures");
				}
			}
		}



		private ObservableCollection<CustomMenuItem> _GestureLibraryMenuItems = new ObservableCollection<CustomMenuItem>();
		public ObservableCollection<CustomMenuItem> GestureLibraryMenuItems
		{
			get { return _GestureLibraryMenuItems; }
			set
			{
				_GestureLibraryMenuItems = value;
				OnPropertyChanged("GestureLibraryMenuItems");
			}
		}

		public User ActiveUser { get { return _config.ActiveUser; } }

		private string _RecognizedGesture;
		public string RecognizedGesture 
		{
			get { return _RecognizedGesture; }
			set
			{
				_RecognizedGesture = value;
				OnPropertyChanged("RecognizedGesture");
			}
		}


		#region Options
		public bool ShowAxes
		{
			get { return _config.BoolOptions[Constants.BoolOptionsNames.ShowAxes]; }
		}

		public bool ShowArms
		{
			get { return _config.BoolOptions[Constants.BoolOptionsNames.ShowArms]; }
		}
		#endregion

		#endregion

		#region Public Methods

		#region Event Handling Methods
		public void OnClosing(object sender, CancelEventArgs e)
		{
			_controller.RemoveListener(_listener);
			_controller.Dispose();
			_listener.Dispose();
		}

		public void OnMouseWheel(object sender, MouseWheelEventArgs e)
		{
			_camera.Zoom(e.Delta);
		}


		private Point initMiddleClickPosition = new Point();
		// I need to look at the relative position (angle) and not just equally rotate if any X / Y change. 
		public void OnMouseMove(object sender, MouseEventArgs e)
		{
			if (e.MiddleButton == MouseButtonState.Pressed)
			{
				UIElement openGLWindow = e.Source as UIElement;
				if (openGLWindow == null) return; // This probably shouldn't happen, but just in case.

				// Check if cursor has gone off screen
				if (!openGLWindow.IsMouseDirectlyOver)
				{
					_camera.Yaw = 0;
					_camera.Pitch = 0;
				}

				//Point center = new Point(openGLWindow.RenderSize.Width / 2.0, openGLWindow.RenderSize.Height / 2.0);
				Point center = initMiddleClickPosition;
				Point position = e.GetPosition(openGLWindow);

				float deltaX = (float) Math.Abs(initMiddleClickPosition.X - position.X);
				float deltaY = (float) Math.Abs(initMiddleClickPosition.Y - position.Y);

				float scaleFactor = 0.05f;
				deltaX *= scaleFactor;
				deltaY *= scaleFactor;

				// Handle X movement
				if (position.X < center.X) 
				{
					_camera.Yaw = -1;
				}
				else if (position.X > center.X)
				{
					_camera.Yaw = 1;
				}
				else
				{
					_camera.Yaw = 0;
				}
				_camera.Yaw *= deltaX;

				// Handle Y movement
				if (position.Y < center.Y)
				{
					_camera.Pitch = -1;
				}
				else if (position.Y > center.Y)
				{
					_camera.Pitch = 1;
				}
				else
				{
					_camera.Pitch = 0;
				}
				_camera.Pitch *= deltaY;
			}
		}

		public void OnMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Middle)
			{
				UIElement openGLWindow = e.Source as UIElement;
				initMiddleClickPosition = e.GetPosition(openGLWindow);
			}
		} 

		public void OnMouseUp(object sender, MouseButtonEventArgs e) 
		{
			if (e.ChangedButton == MouseButton.Middle)
			{
				// Might want to check if the arrow (left, right, up, down) keys are pressed, and ignore if so.
				_camera.Yaw = 0;
				_camera.Pitch = 0;
			}
		}

		public void OnMouseLeaveOpenGLWindow(object sender, MouseEventArgs e)
		{
			_camera.Yaw = 0;
			_camera.Pitch = 0;
		}

		public void OnMouseEnterOpenGLWindow(object sender, MouseEventArgs e)
		{
			if (e.MiddleButton == MouseButtonState.Pressed)
			{
				UIElement openGLWindow = e.Source as UIElement;
				initMiddleClickPosition = e.GetPosition(openGLWindow);
			}
		}

		public void OnKeyDown(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				//case Key.Left:
				//	_camera.Yaw = 1;
				//	break;
				//case Key.Right:
				//	_camera.Yaw = -1;
				//	break;
				//case Key.Up:
				//	_camera.Pitch = 1;
				//	break;
				//case Key.Down:
				//	_camera.Pitch = -1;
				//	break;
				//case Key.LeftCtrl:
				//	_camera.Roll = 1;
				//	break;
				//case Key.RightCtrl:
				//	_camera.Roll = -1;
				//	break;

				case Key.Left:
					dynamicGestureStep--;
					break;
				case Key.Right:
					dynamicGestureStep++;
					break;

				case Key.NumPad1:
					dynamicGestureStep++;
					break;
				case Key.NumPad0:
					if(dynamicGestureStep > 0) dynamicGestureStep--;
					break;

				// End the dynamic gesture recording:
				case Key.Enter:
					if (Mode == LGR_Mode.Debug)
					{
						var instances = DynamicGestureRecorder.Instances;
					}
					break;
			}
		}

		public void OnKeyUp(object sender, KeyEventArgs e)
		{
			switch (e.Key)
			{
				case Key.Left:
				case Key.Right:
					_camera.Yaw = 0;
					break;
				case Key.Up:
				case Key.Down:
					_camera.Pitch = 0;
					break;
				case Key.LeftCtrl:
				case Key.RightCtrl:
					_camera.Roll = 0;
					break;
			}
		}
		#endregion


		//public void RecordGestureInstances(EditStaticGestureViewModel editGestureVM) // Maybe add delay and numInstances to parameters
		//{
		//	var recorder = new StaticGestureRecorder(this, editGestureVM);
		//	recorder.RecordGestureInstancesWithCountdown(5, 500, 10);
		//}

		// Measures hand on screen
		public HandMeasurements MeasureHand()
		{
			Hand hand = _controller.Frame().Hands.FirstOrDefault();
			if (hand == null) return null;
			return new StaticGestureInstanceSingleHand(hand).GetMeasurements();
		}

		public void DrawScene() // This is really the main loop of the entire program. Should maybe rename.
		{
			//  Clear the color and depth buffer.
			_gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

			if (_camera.ShouldRotate) _camera.Revolve();
			if (ShowAxes) _glHelper.DrawAxes();

			CurrentFrame = _controller.Frame();

			OnFrameReceived(CurrentFrame);

			switch (Mode)
			{
				case LGR_Mode.EditStatic:
					if(SelectedStaticGesture != null && !_editStaticGestureControl.VM.RecordingInProgress) _glHelper.DrawStaticGesture(SelectedStaticGesture, ShowArms);
					else _glHelper.DrawFrame(CurrentFrame, ShowArms);
					break;
				case LGR_Mode.EditDynamic:
					
					if (_editDynamicGestureControl.VM.RecordingInProgress)
					{
						//DynamicGestureRecorder.ProcessFrame(CurrentFrame);
						//ClearOutputWindow();
						//WriteLineToOutputWindow("Dynamic Gesture Recorder Debug Info:");
						//WriteLineToOutputWindow(DynamicGestureRecorder.DebugMessage);
						_editDynamicGestureControl.VM.ProcessFrame(CurrentFrame);
						_glHelper.DrawFrame(CurrentFrame, ShowArms);
					}
					else
					{
						if (SelectedDynamicGesture != null)
						{
							int sampleIndex = dynamicGestureStep % SelectedDynamicGesture.Samples.Count;
							_glHelper.DrawStaticGesture(SelectedDynamicGesture.Samples[sampleIndex], ShowArms);
						}
						else _glHelper.DrawFrame(CurrentFrame, ShowArms);
					}
					
					break;
				case LGR_Mode.Recognize:
					_glHelper.DrawFrame(CurrentFrame, ShowArms);
					_recognitionMonitorControl.VM.ProcessFrame(CurrentFrame);
					break;
				case LGR_Mode.Debug:
					_glHelper.DrawFrame(CurrentFrame, ShowArms);
					DynamicGestureRecorder.ProcessFrame(CurrentFrame);
					ClearOutputWindow();
					WriteLineToOutputWindow("Dynamic Gesture Recorder Debug Info:");
					WriteLineToOutputWindow(DynamicGestureRecorder.DebugMessage);
					break;
				default:
					_glHelper.DrawFrame(CurrentFrame, ShowArms);
					break;
			}
		}

		public void ViewStaticGesture(StaticGestureInstance gestureInstance)
		{
			Mode = LGR_Mode.EditStatic;
			SelectedStaticGesture = gestureInstance;
		}

		private int dynamicGestureStep = 0;
		public void ViewDynamicGesture(DynamicGestureInstance gestureInstance)
		{
			Mode = LGR_Mode.EditDynamic;
			SelectedDynamicGesture = gestureInstance;
			dynamicGestureStep = 0;
		}

		#region Dialog Windows
		public void DisplayOptionsDialog()
		{
			OptionsDialog optionsDialog = new OptionsDialog(this);
			if (optionsDialog.ShowDialog() == true)
			{
				// General Options
				foreach (var boolOption in optionsDialog.Changeset.BoolOptionsChangeset)
				{
					_sqliteProvider.UpdateBoolOption(boolOption.Key, boolOption.Value);
					_config.BoolOptions[boolOption.Key] = boolOption.Value;
				}
				// Bone Colors
				foreach (var boneColor in optionsDialog.Changeset.BoneColorsChangeset)
				{
					_sqliteProvider.UpdateBoneColor(boneColor.Key, boneColor.Value);
					_config.BoneColors[boneColor.Key] = boneColor.Value;
				}
				// Users
				User newActiveUser = optionsDialog.Changeset.ActiveUser;
				if (newActiveUser != null && newActiveUser.Id != _config.ActiveUser.Id)
				{
					_sqliteProvider.SetActiveUser(newActiveUser.Id);
					_config.ActiveUser = newActiveUser;
					OnPropertyChanged("ActiveUser");
				}
				foreach (var newUser in optionsDialog.Changeset.NewUsers)
				{
					_sqliteProvider.SaveNewUser(newUser);
				}
				foreach (var modifiedUser in optionsDialog.Changeset.ModifiedUsers)
				{
					_sqliteProvider.UpdateUser(modifiedUser);
				}
				foreach (int userId in optionsDialog.Changeset.DeletedUserIds)
				{
					_sqliteProvider.DeleteUser(userId);
				}

				_config.AllUsers = _sqliteProvider.GetAllUsers(); // Just to make sure it's up to date  
			}
		}

		public void NewStaticGesture()
		{
			EditStaticGesture(new StaticGestureClassWrapper(), newGesture: true);
		}

		public void EditStaticGesture(StaticGestureClassWrapper gesture, bool newGesture = false)
		{
			_editStaticGestureControl.VM = new EditStaticGestureViewModel(this, gesture, newGesture);
			Mode = LGR_Mode.EditStatic;
			if (!newGesture) ViewStaticGesture(gesture.SampleInstance);
		}

		public void NewDynamicGesture()
		{
			EditDynamicGesture(new DynamicGestureClassWrapper(), newGesture: true);
		}

		public void EditDynamicGesture(DynamicGestureClassWrapper gesture, bool newGesture = false)
		{
			_editDynamicGestureControl.VM = new EditDynamicGestureViewModel(this, gesture, newGesture);
			Mode = LGR_Mode.EditDynamic;
			if (!newGesture) ViewDynamicGesture(gesture.SampleInstance);
		}
		#endregion

		#region Output Window
		public static void WriteLineToOutputWindow(string message)
		{
			WriteToOutputWindow(message + "\n");
		}

		public static void WriteToOutputWindow(string message)
		{
			// TODO: Might want to restrict the size of log
			OutputWindowContent += "> " + message;
		}

		public static void ClearOutputWindow()
		{
			OutputWindowContent = "";
		}
		#endregion

		public void InitOpenGL(OpenGL openGL)
		{
			//  Set the clear color.
			_gl.ClearColor(0, 0, 0, 0);

			_gl.Enable(OpenGL.GL_BLEND);
			_gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
		}

		public void HandleResize(double width, double height)
		{
			_camera.Width = width;
			_camera.Height = height;
			_camera.UpdateView();
		}

		public void UpdateStaticGestureLibrary()
		{
			StaticGestures = _sqliteProvider.GetAllStaticGestureClasses();
		}

		public void UpdateDynamicGestureLibrary()
		{
			DynamicGestures = _sqliteProvider.GetAllDynamicGestureClasses();
		}

		public void UpdateClassifier()
		{
			_classifier = new StatisticalClassifier(StaticGestures, DynamicGestures);
		}
		#endregion

		#region Private Methods
		private void initMenuBar()
		{
			MenuBar = new ObservableCollection<CustomMenuItem>();

			#region RESET CAMERA
			CustomMenuItem resetCamera = new CustomMenuItem("Reset Camera");
			resetCamera.Command = new CustomCommand(a => _camera.Reset());
			MenuBar.Add(resetCamera);
			#endregion

			#region DEFAULT MODE
			// Might want to rename to "Live Mode"
			CustomMenuItem defaultMode = new CustomMenuItem("Default Mode");
			defaultMode.Command = new CustomCommand(a => Mode = LGR_Mode.Default);
			MenuBar.Add(defaultMode);
			#endregion

			#region RECOGNIZE MODE
			CustomMenuItem recognizeMode = new CustomMenuItem("Recognize Mode");
			recognizeMode.Command = new CustomCommand(a => Mode = LGR_Mode.Recognize);
			MenuBar.Add(recognizeMode);
			#endregion

			#region DEBUG MODE
			CustomMenuItem debugMode = new CustomMenuItem("Debug Mode");
			debugMode.Command = new CustomCommand(a => Mode = LGR_Mode.Debug);
			MenuBar.Add(debugMode);
			#endregion

			#region OPTIONS
			CustomMenuItem options = new CustomMenuItem("Options");
			options.Command = new CustomCommand(a => DisplayOptionsDialog());
			MenuBar.Add(options);
			#endregion

			#region NEW STATIC GESTURE
			CustomMenuItem newStaticGesture = new CustomMenuItem("New Static Gesture");
			newStaticGesture.Command = new CustomCommand(a => NewStaticGesture());
			MenuBar.Add(newStaticGesture);
			#endregion

			#region NEW DYNAMIC GESTURE
			CustomMenuItem newDynamicGesture = new CustomMenuItem("New Dynamic Gesture");
			newDynamicGesture.Command = new CustomCommand(a => NewDynamicGesture());
			MenuBar.Add(newDynamicGesture);
			#endregion
		}
		#endregion

		#region PropertyChanged
		public event PropertyChangedEventHandler PropertyChanged;

		protected void OnPropertyChanged(string name)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null)
			{
				handler(this, new PropertyChangedEventArgs(name));
			}
		}
		#endregion

	}

}
