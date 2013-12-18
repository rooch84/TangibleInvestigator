
// This demo app displays a series of images on your Sifteo cubes. Different
// gestures trigger different actions on the images:
//
// * Press: Switch to the next image
// * Neighbor: rotate the image
// * Tilt: offset the image
// * Shake: rotate the image randomly
// * Flip: scale the image
//
// This program demonstrates the following concepts:
//
// * Event handling basics
// * Cube sensor events
// * Image drawing basics
//
// In addition to illustrating APIs and programming concepts, this demo can be
// a useful utility for quickly testing graphics. Just drop your images into
// this project's `assets/images` directory, bundle your assets in the
// ImageHelper tool, and reload the game to check them out.

// ------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using Sifteo;

namespace SlideShow
{
	public class SlideShowApp : BaseApp
	{
		public String[] mImageNames;
		public List<CubeWrapper> mWrappers = new List<CubeWrapper> ();
		public Random mRandom = new Random ();
		// Here we initialize our app.
		public override void Setup ()
		{

			AMQConnector amqConn = new AMQConnector ();
			amqConn.Connect ();

			// Load up the list of images.
			mImageNames = LoadImageIndex ();

			// Loop through all the cubes and set them up.
			foreach (Cube cube in CubeSet) {

				// Create a wrapper object for each cube. The wrapper object allows us
				// to bundle a cube with extra information and behavior.
				CubeWrapper wrapper = new CubeWrapper (this, cube, amqConn);
				mWrappers.Add (wrapper);
				wrapper.DrawSlide ();
			}

			// ## Event Handlers ##
			// Objects in the Sifteo API (particularly BaseApp, CubeSet, and Cube)
			// fire events to notify an app of various happenings, including actions
			// that the player performs on the cubes.
			//
			// To listen for an event, just add the handler method to the event. The
			// handler method must have the correct signature to be added. Refer to
			// the API documentation or look at the examples below to get a sense of
			// the correct signatures for various events.
			//
			// **NeighborAddEvent** and **NeighborRemoveEvent** are triggered when
			// the player puts two cubes together or separates two neighbored cubes.
			// These events are fired by CubeSet instead of Cube because they involve
			// interaction between two Cube objects. (There are Cube-level neighbor
			// events as well, which comes in handy in certain situations, but most
			// of the time you will find the CubeSet-level events to be more useful.)
			CubeSet.NeighborAddEvent += OnNeighborAdd;
			CubeSet.NeighborRemoveEvent += OnNeighborRemove;
		}
		// ## Neighbor Add ##
		// This method is a handler for the NeighborAdd event. It is triggered when
		// two cubes are placed side by side.
		//
		// Cube1 and cube2 are the two cubes that are involved in this neighboring.
		// The two cube arguments can be in any order; if your logic depends on
		// cubes being in specific positions or roles, you need to add logic to
		// this handler to sort the two cubes out.
		//
		// Side1 and side2 are the sides that the cubes neighbored on.
		private void OnNeighborAdd (Cube cube1, Cube.Side side1, Cube cube2, Cube.Side side2)
		{
			Log.Debug ("Neighbor add: {0}.{1} <-> {2}.{3}", cube1.UniqueId, side1, cube2.UniqueId, side2);

			CubeWrapper wrapper = (CubeWrapper)cube1.userData;
			if (wrapper != null) {
				// Here we set our wrapper's rotation value so that the image gets
				// drawn with its top side pointing towards the neighbor cube.
				//
				// Cube.Side is an enumeration (TOP, LEFT, BOTTOM, RIGHT, NONE). The
				// values of the enumeration can be cast to integers by counting
				// counterclockwise:
				//
				// * TOP = 0
				// * LEFT = 1
				// * BOTTOM = 2
				// * RIGHT = 3
				// * NONE = 4
				wrapper.mRotation = (int)side1;
				wrapper.mNeedDraw = true;
			}

			wrapper = (CubeWrapper)cube2.userData;
			if (wrapper != null) {
				wrapper.mRotation = (int)side2;
				wrapper.mNeedDraw = true;
			}

		}
		// ## Neighbor Remove ##
		// This method is a handler for the NeighborRemove event. It is triggered
		// when two cubes that were neighbored are separated.
		//
		// The side arguments for this event are the sides that the cubes
		// _were_ neighbored on before they were separated. If you check the
		// current state of their neighbors on those sides, they should of course
		// be NONE.
		private void OnNeighborRemove (Cube cube1, Cube.Side side1, Cube cube2, Cube.Side side2)
		{
			Log.Debug ("Neighbor remove: {0}.{1} <-> {2}.{3}", cube1.UniqueId, side1, cube2.UniqueId, side2);

			CubeWrapper wrapper = (CubeWrapper)cube1.userData;
			if (wrapper != null) {
				wrapper.mScale = 1;
				wrapper.mRotation = 0;
				wrapper.mNeedDraw = true;
			}

			wrapper = (CubeWrapper)cube2.userData;
			if (wrapper != null) {
				wrapper.mScale = 1;
				wrapper.mRotation = 0;
				wrapper.mNeedDraw = true;
			}
		}
		// Defer all per-frame logic to each cube's wrapper.
		public override void Tick ()
		{
			foreach (CubeWrapper wrapper in mWrappers) {
				wrapper.Tick ();
			}
		}
		// ImageSet is an enumeration of your app's images. It is populated based
		// on your app's siftbundle and index. You rarely have to interact with it
		// directly, since you can refer to images by name.
		//
		// In this method, we scan the image set to build an array with the names
		// of all the images.
		private String[] LoadImageIndex ()
		{
			ImageSet imageSet = this.Images;
			ArrayList nameList = new ArrayList ();
			foreach (ImageInfo image in imageSet) {
				nameList.Add (image.name);
			}
			String[] rv = new String[nameList.Count];
			for (int i = 0; i < nameList.Count; i++) {
				rv [i] = (string)nameList [i];
			}
			return rv;
		}
	}
	// ------------------------------------------------------------------------
	// ## Wrapper ##
	// "Wrapper" is not a specific API, but a pattern that is used in many Sifteo
	// apps. A wrapper is an object that bundles a Cube object with game-specific
	// data and behaviors.
	public class CubeWrapper
	{
		public SlideShowApp mApp;
		public Cube mCube;
		public int mIndex;
		public int mXOffset = 0;
		public int mYOffset = 0;
		public int mScale = 1;
		public int mRotation = 0;
		// This flag tells the wrapper to redraw the current image on the cube. (See Tick, below).
		public bool mNeedDraw = false;

		private AMQConnector amqConn;

		public CubeWrapper (SlideShowApp app, Cube cube, AMQConnector amq)
		{
			mApp = app;
			mCube = cube;
			mCube.userData = this;
			mIndex = 0;
			amqConn = amq;

			// Here we attach more event handlers for button and accelerometer actions.
			mCube.ButtonEvent += OnButton;
			mCube.TiltEvent += OnTilt;
			mCube.ShakeStartedEvent += OnShakeStarted;
			mCube.ShakeStoppedEvent += OnShakeStopped;
			mCube.FlipEvent += OnFlip;
		}
		// ## Button ##
		// This is a handler for the Button event. It is triggered when a cube's
		// face button is either pressed or released. The `pressed` argument
		// is true when you press down and false when you release.
		private void OnButton (Cube cube, bool pressed)
		{
			if (pressed) {
				Log.Debug ("Button pressed");
			} else {
				Log.Debug ("Button released");

				// Advance the image index so that the next image is drawn on this
				// cube.
				this.mIndex += 1;
				if (mIndex >= mApp.mImageNames.Length) {
					mIndex = 0;
				}
				mRotation = 0;
				mScale = 1;
				mNeedDraw = true;
				amqConn.send ();
			}
		}
		// ## Tilt ##
		// This is a handler for the Tilt event. It is triggered when a cube is
		// tilted past a certain threshold. The x, y, and z arguments are filtered
		// values for the cube's three-axis acceleromter. A tilt event is only
		// triggered when the filtered value changes, i.e., when the accelerometer
		// crosses certain thresholds.
		private void OnTilt (Cube cube, int tiltX, int tiltY, int tiltZ)
		{
			Log.Debug ("Tilt: {0} {1} {2}", tiltX, tiltY, tiltZ);

			// If the X axis tilt reads 0, the cube is tilting to the left. <br/>
			// If it reads 1, the cube is centered. <br/>
			// If it reads 2, the cube is tilting to the right.
			if (tiltX == 0) {
				mXOffset = -8;
			} else if (tiltX == 1) {
				mXOffset = 0;
			} else if (tiltX == 2) {
				mXOffset = 8;
			}

			// If the Y axis tilt reads 0, the cube is tilting down. <br/>
			// If it reads 1, the cube is centered. <br/>
			// If it reads 2, the cube is tilting up.
			if (tiltY == 0) {
				mYOffset = 8;
			} else if (tiltY == 1) {
				mYOffset = 0;
			} else if (tiltY == 2) {
				mYOffset = -8;
			}

			// If the Z axis tilt reads 2, the cube is face up. <br/>
			// If it reads 1, the cube is standing on a side. <br/>
			// If it reads 0, the cube is face down.
			if (tiltZ == 1) {
				mXOffset *= 2;
				mYOffset *= 2;
			}

			mNeedDraw = true;
		}
		// ## Shake Started ##
		// This is a handler for the ShakeStarted event. It is triggered when the
		// player starts shaking a cube. When the player stops shaking, a
		// corresponding ShakeStopped event will be fired (see below).
		//
		// Note: while a cube is shaking, it will still fire tilt and flip events
		// as its internal accelerometer goes around and around. If your game wants
		// to treat shaking separately from tilting or flipping, you need to add
		// logic to filter events appropriately.
		private void OnShakeStarted (Cube cube)
		{
			Log.Debug ("Shake start");
		}
		// ## Shake Stopped ##
		// This is a handler for the ShakeStarted event. It is triggered when the
		// player stops shaking a cube. The `duration` argument tells you
		// how long (in milliseconds) the cube was shaken.
		private void OnShakeStopped (Cube cube, int duration)
		{
			Log.Debug ("Shake stop: {0}", duration);
			mRotation = 0;
			mNeedDraw = true;
		}
		// ## Flip ##
		// This is a handler for the Flip event. It is triggered when the player
		// turns a cube face down or face up. The `newOrientationIsUp` argument
		// tells you which way the cube is now facing.
		//
		// Note that when a Flip event is triggered, a Tilt event is also
		// triggered.
		private void OnFlip (Cube cube, bool newOrientationIsUp)
		{
			if (newOrientationIsUp) {
				Log.Debug ("Flip face up");
				mScale = 1;
				mNeedDraw = true;
			} else {
				Log.Debug ("Flip face down");
				mScale = 2;
				mNeedDraw = true;
			}
		}
		// ## Cube.Image ##
		// This method draws the current image to the cube's display. The
		// Cube.Image method has a lot of arguments, but many of them are optional
		// and have reasonable default values.
		public void DrawSlide ()
		{

			// Here we specify the name of the image to draw, in this case by pulling
			// it from the array of names we read out of the image set (see
			// LoadImageIndex, above).
			//
			// When specifying the image name, leave off any file type extensions
			// (png, gif, etc). Refer to the index file that ImageHelper generates
			// during asset conversion.
			//
			// If you specify an image name that is not in the index, the Image call
			// will be ignored.
			String imageName = this.mApp.mImageNames [this.mIndex];

			// You can specify the top/left point on the screen to start drawing at.
			int screenX = mXOffset;
			int screenY = mYOffset;

			// You can draw a portion of an image by specifying coordinates to start
			// reading from (top/left). In this case, we're just going to draw the
			// whole image every time.
			int imageX = 0;
			int imageY = 0;

			// You should always specify the width and height of the image to be
			// drawn. If you specify values that are less than the size of the image,
			// only the portion you specify will be drawn. If you specify values
			// larger than the image, the behavior is undefined (so don't do that).
			//
			// In this example, we assume that the image is 128x128, big enough to
			// cover the full size of the display. If the image runs off the sides of
			// the display (because of offsets due to tilting; see OnTilt, above), it
			// will be clipped.
			int width = 128;
			int height = 128;

			// You can upscale an image by integer multiples. A scaled image still
			// starts drawing at the specified top/left point, but the area of the
			// display it covers (width/height) will be multipled by the scale.
			//
			// The default value is 1 (1:1 scale).
			int scale = mScale;

			// You can rotate an image by quarters. The rotation value is an integer
			// representing counterclockwise rotation.
			//
			// * 0 = no rotation
			// * 1 = 90 degrees counterclockwise
			// * 2 = 180 degrees
			// * 3 = 90 degrees clockwise
			//
			// A rotated image still starts drawing at the specified top/left point;
			// the pixels are just drawn in rotated order.
			//
			// The default value is 0 (no rotation).
			int rotation = mRotation;

			// Clear off whatever was previously on the display before drawing the new image.
			mCube.FillScreen (Color.Black);

			mCube.Image (imageName, screenX, screenY, imageX, imageY, width, height, scale, rotation);

			// Remember: always call Paint if you actually want to see anything on the cube's display.
			mCube.Paint ();
		}
		// This method is called every frame by the Tick in SlideShowApp (see above.)
		public void Tick ()
		{

			// You can check whether a cube is being shaken at this moment by looking
			// at the IsShaking flag.
			if (mCube.IsShaking) {
				mRotation = mApp.mRandom.Next (4);
				mNeedDraw = true;
			}

			// If anyone has raised the mNeedDraw flag, redraw the image on the cube.
			if (mNeedDraw) {
				mNeedDraw = false;
				DrawSlide ();
			}
		}
	}
}
// -----------------------------------------------------------------------
//
// SlideShowApp.cs
//
// Copyright &copy; 2011 Sifteo Inc.
//
// This program is "Sample Code" as defined in the Sifteo
// Software Development Kit License Agreement. By adapting
// or linking to this program, you agree to the terms of the
// License Agreement.
//
// If this program was distributed without the full License
// Agreement, a copy can be obtained by contacting
// support@sifteo.com.
//

