using System;
using Godot;

public partial class PlatformerController2D : CharacterBody2D
{
	[Signal]
	public delegate void jumpedEventHandler(bool isGroundJump);

	[Signal]
	public delegate void hitGroundEventHandler();

	// Set these to the name of your action (in the Input Map)
	// Name of input action to move left.
	[Export]
	public string InputLeft = "move_left";
	// Name of input action to move right.
	[Export]
	public string InputRight = "move_right";
	// Name of input action to jump.
	[Export]
	public string InputJump = "jump";

	private const float DEFAULT_MAX_JUMP_HEIGHT = 150.0f;
	private const float DEFAULT_MIN_JUMP_HEIGHT = 60.0f;
	private const float DEFAULT_DOUBLE_JUMP_HEIGHT = 100.0f;
	private const float DEFAULT_JUMP_DURATION = 0.3f;

	private float _maxJumpHeight =  DEFAULT_MAX_JUMP_HEIGHT;

	// The max jump height in pixels (holding jump).
	[Export]
	public float MaxJumpHeight 
	{
		get { return _maxJumpHeight; }
		set 
		{
			_maxJumpHeight = value;
		
			_defaultGravity = CalculateGravity(_maxJumpHeight, _jumpDuration);
			_jumpVelocity = CalculateJumpVelocity(_maxJumpHeight, _jumpDuration);
			_doubleJumpVelocity = CalculateJumpVelocity2(DoubleJumpHeight, _defaultGravity);
			_releaseGravityMultiplier = CalculateReleaseGravityMultiplier(
					_jumpVelocity, MinJumpHeight, _defaultGravity);
		}
	}

	private float _minJumpHeight = DEFAULT_MIN_JUMP_HEIGHT;

	[Export]
	// The minimum jump height (tapping jump).
	public float MinJumpHeight
	{
		get { return _minJumpHeight; }
		set 
		{
			_minJumpHeight = value;
			_releaseGravityMultiplier = CalculateReleaseGravityMultiplier(
					_jumpVelocity, _minJumpHeight, _defaultGravity);
		}
	}

	private  float _doubleJumpHeight = DEFAULT_DOUBLE_JUMP_HEIGHT;
	// The height of your jump in the air.
	[Export]
	public float DoubleJumpHeight
	{
		get { return _doubleJumpHeight; }
		set
		{
			_doubleJumpHeight = value;
			_doubleJumpVelocity = CalculateJumpVelocity2(_doubleJumpHeight, _defaultGravity);
		}
	}

	private float _jumpDuration = DEFAULT_JUMP_DURATION;
	// How long it takes to get to the peak of the jump in seconds.
	[Export]
	public float JumpDuration
	{
		get { return _jumpDuration; }
		set
		{
			_jumpDuration = value;

			_defaultGravity = CalculateGravity(MaxJumpHeight, _jumpDuration);
			_jumpVelocity = CalculateJumpVelocity(MaxJumpHeight, _jumpDuration);
			_doubleJumpVelocity = CalculateJumpVelocity2(DoubleJumpHeight, _defaultGravity);
			_releaseGravityMultiplier = CalculateReleaseGravityMultiplier(
					_jumpVelocity, MinJumpHeight, _defaultGravity);
		}
	}

	// Multiplies the gravity by this while falling.
	[Export]
	public float FallingGravityMultiplier = 1.5f;
	// Amount of jumps allowed before needing to touch the ground again. Set to 2 for double jump.
	[Export]
	public int MaxJumpAmount = 1;
	[Export]
	public float MaxAcceleration = 10000.0f;
	[Export]
	public float Friction = 20.0f;
	[Export]
	public bool CanHoldJump = false;
	// You can still jump this many seconds after falling off a ledge.
	[Export]
	public float CoyoteTime = 0.1f;
	// Pressing jump this many seconds before hitting the ground will still make you jump.
	// Only neccessary when can_hold_jump is unchecked.
	[Export]
	public float JumpBuffer = 0.1f;

	// These will be calcualted automatically
	// Gravity will be positive if it's going down, and negative if it's going up
	private float _defaultGravity;
	private float _jumpVelocity;
	private float _doubleJumpVelocity;
	// Multiplies the gravity by this when we release jump
	private float _releaseGravityMultiplier;

	private int _jumpsLeft;
	private bool _holdingJump = false;

	enum JumpType {NONE, GROUND, AIR};
	// The type of jump the player is performing. Is JumpType.NONE if they player is on the ground.
	private JumpType _currentJumpType = JumpType.NONE;

	// Used to detect if player just hit the ground
	private bool _wasOnGround;

	private Vector2 _acceleration = new Vector2();

	// coyote_time and jump_buffer must be above zero to work. Otherwise, godot will throw an error.
	private bool _isCoyoteTimeEnabled;
	private bool _isJumpBufferEnabled;
	private Timer _coyoteTimer = new Timer();
	private Timer _jumpBufferTimer = new Timer();

	public void Init()
	{
		_defaultGravity = CalculateGravity(MaxJumpHeight, _jumpDuration);
		_jumpVelocity = CalculateJumpVelocity(MaxJumpHeight, _jumpDuration);
		_doubleJumpVelocity = CalculateJumpVelocity2(DoubleJumpHeight, _defaultGravity);
		_releaseGravityMultiplier = CalculateReleaseGravityMultiplier(
				_jumpVelocity, MinJumpHeight, _defaultGravity);
	}

	public override void _Ready()
	{
		Init();
		
		// @onready variables
		_isCoyoteTimeEnabled = CoyoteTime > 0.0f;
		_isJumpBufferEnabled = JumpBuffer > 0.0f;
	
		if (_isCoyoteTimeEnabled)
		{
			AddChild(_coyoteTimer);
			_coyoteTimer.WaitTime = CoyoteTime;
			_coyoteTimer.OneShot = true;
		}

		if (_isJumpBufferEnabled)
		{
			AddChild(_jumpBufferTimer);
			_jumpBufferTimer.WaitTime = JumpBuffer;
			_jumpBufferTimer.OneShot = true;
		}
	}

	public override void _Input(InputEvent _event)
	{
		_acceleration.X = 0.0f;
		if (Input.IsActionPressed(InputLeft))
		{
			_acceleration.X = -MaxAcceleration;
		}
		
		if (Input.IsActionPressed(InputRight))
		{
			_acceleration.X = MaxAcceleration;
		}
		
		if (Input.IsActionJustPressed(InputJump))
		{
			_holdingJump = true;
			StartJumpBufferTimer();
			if ((!CanHoldJump && CanGroundJump()) || CanDoubleJump())
			{
				Jump();
			}
		}
			
		if (Input.IsActionJustReleased(InputJump))
		{
			_holdingJump = false;
		}
	}

	// rename delta to _delta
	public override void _PhysicsProcess(double delta)
	{
		try
		{
			if (IsCoyoteTimerRunning() || (_currentJumpType == JumpType.NONE))
			{
				_jumpsLeft = MaxJumpAmount;
			}
			
			if (IsFeetOnGround() && (_currentJumpType == JumpType.NONE))
			{
				StartCoyoteTimer();
			}
				
			// Check if we just hit the ground this frame

			if (! _wasOnGround && IsFeetOnGround())
			{
				_currentJumpType = JumpType.NONE;
				if (IsJumpBufferTimerRunning() && CanHoldJump)
				{
					Jump();
				}
				
				EmitSignal(SignalName.hitGround);
			}
			
			// Cannot do this in _input because it needs to be checked every frame
			if (Input.IsActionPressed(InputJump))
			{
				if (CanGroundJump() && CanHoldJump)
				{
					Jump();
				}
			}

			float gravity = ApplyGravityMultipliersTo(_defaultGravity);
			_acceleration.Y = gravity;
			
			// Apply friction
			Vector2 newVelocity = Velocity;
			newVelocity.X *= 1.0f / (1.0f + ((float)delta * Friction));
			newVelocity += _acceleration * (float)delta;
			Velocity = newVelocity;

			_wasOnGround = IsFeetOnGround();
			MoveAndSlide();
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Exception in _UnhandledInput: {ex.Message}\n{ex.StackTrace}");
			throw; // Rethrow the exception to stop the application
		}
	}

	// Use this instead of coyote_timer.start() to check if the coyote_timer is enabled first
	private void StartCoyoteTimer()
	{
		if (_isCoyoteTimeEnabled)
		{
			_coyoteTimer.Start();
		}
	}

	// Use this instead of jump_buffer_timer.start() to check if the jump_buffer is enabled first
	private void StartJumpBufferTimer()
	{
		if (_isJumpBufferEnabled)
		{
			_jumpBufferTimer.Start();
		}
	}

	// Use this instead of `not coyote_timer.is_stopped()`. This will always return false if 
	// the coyote_timer is disabled
	private bool IsCoyoteTimerRunning()
	{
		if (_isCoyoteTimeEnabled && !_coyoteTimer.IsStopped())
		{
			return true;
		}
		
		return false;
	}

	// Use this instead of `not jump_buffer_timer.is_stopped()`. This will always return false if 
	// the jump_buffer_timer is disabled
	private bool IsJumpBufferTimerRunning()
	{
		if (_isJumpBufferEnabled && !_jumpBufferTimer.IsStopped())
		{
			return true;
		}
		
		return false;
	}

	private bool CanGroundJump()
	{
		if ((_jumpsLeft > 0) && IsFeetOnGround() && (_currentJumpType == JumpType.NONE))
		{
			return true;
		}
		else if (IsCoyoteTimerRunning())
		{
			return true;
		}
		
		return false;
	}

	private bool CanDoubleJump()
	{
		if ((_jumpsLeft <= 1) && (_jumpsLeft == MaxJumpAmount))
		{
			// Special case where you've fallen off a cliff and only have 1 jump. You cannot use your
			// first jump in the air
			return false;
		}

		if ((_jumpsLeft > 0) && (!IsFeetOnGround()) && _coyoteTimer.IsStopped())
		{
			return true;
		}

		return false;
	}

	// Same as is_on_floor(), but also returns true if gravity is reversed and you are on the ceiling
	private bool IsFeetOnGround()
	{
		if (IsOnFloor() && (_defaultGravity >= 0.0f))
		{
			return true;
		}
		
		if (IsOnCeiling() && (_defaultGravity <= 0.0f))
		{
			return true;
		}

		return false;
	}
	
	// Perform a ground jump, or a double jump if the character is in the air.
	private void Jump()
	{
		if (CanDoubleJump())
		{
			DoubleJump();
		}
		else
		{
			GroundJump();
		}
	}

	// Perform a double jump without checking if the player is able to.
	private void DoubleJump()
	{
		if (_jumpsLeft == MaxJumpAmount)
		{
			// Your first jump must be used when on the ground.
			// If your first jump is used in the air, an additional jump will be taken away.
			_jumpsLeft -= 1;
		}
		

		Velocity = new Vector2(Velocity.X, Velocity.Y - _doubleJumpVelocity);
		_currentJumpType = JumpType.AIR;
		_jumpsLeft -= 1;
		EmitSignal(SignalName.jumped, false);
	}

	// Perform a ground jump without checking if the player is able to.
	private void GroundJump()
	{
		Velocity = new Vector2(Velocity.X, Velocity.Y - _jumpVelocity);
		_currentJumpType = JumpType.GROUND;
		_jumpsLeft -= 1;
		_coyoteTimer.Stop();
		EmitSignal(SignalName.jumped, true);
	}
   
	private float ApplyGravityMultipliersTo(float gravity)
	{
		if (Velocity.Y * Mathf.Sign(_defaultGravity) > 0.0f) // If we are falling
		{
			gravity *= FallingGravityMultiplier;
		}
		// if we released jump and are still rising
		else if (Velocity.Y * Mathf.Sign(_defaultGravity) < 0.0f)
		{
			if (! _holdingJump)
			{
				if (_currentJumpType != JumpType.AIR) // Always jump to max height when we are using a double jump
				{
					gravity *= _releaseGravityMultiplier; // multiply the gravity so we have a lower jump
				}
			}
		}
		
		return gravity;
	}

	// Calculates the desired gravity from jump height and jump duration.  [br]
	// Formula is from [url=https://www.youtube.com/watch?v=hG9SzQxaCm8]this video[/url] 
	private float CalculateGravity(float maxJumpHeight, float jumpDuration)
	{
		return (2.0f * maxJumpHeight) / Mathf.Pow(jumpDuration, 2.0f);
	}

	// Calculates the desired jump velocity from jump height and jump duration.
	private float CalculateJumpVelocity(float maxJumpHeight, float jumpDuration)
	{
		return (2.0f * maxJumpHeight) / (jumpDuration);
	}

	// Calculates jump velocity from jump height and gravity.  [br]
	// Formula from 
	// [url]https://sciencing.com/acceleration-velocity-distance-7779124.html#:~:text=in%20every%20step.-,Starting%20from%3A,-v%5E2%3Du[/url]
	private float CalculateJumpVelocity2(float maxJumpHeight, float gravity)
	{
		return Mathf.Sqrt(Math.Abs(2.0f * gravity * maxJumpHeight)) * Mathf.Sign(maxJumpHeight);
	}

	// Calculates the gravity when the key is released based off the minimum jump height and jump velocity.  [br]
	// Formula is from [url]https://sciencing.com/acceleration-velocity-distance-7779124.html[/url]
	private float CalculateReleaseGravityMultiplier(float jumpVelocity, float minJumpHeight, float gravity)
	{
		float releaseGravity = Mathf.Pow(jumpVelocity, 2.0f) / (2.0f * minJumpHeight);
		return releaseGravity / gravity;
	}

	// Returns a value for friction that will hit the max speed after 90% of time_to_max seconds.  [br]
	// Formula from [url]https://www.reddit.com/r/gamedev/comments/bdbery/comment/ekxw9g4/?utm_source=share&utm_medium=web2x&context=3[/url]
	private float CalculateFriction(float timeToMax)
	{
		return 1.0f - (2.30259f / timeToMax);
	}

	// Formula from [url]https://www.reddit.com/r/gamedev/comments/bdbery/comment/ekxw9g4/?utm_source=share&utm_medium=web2x&context=3[/url]
	public float CalculateSpeed(float maxSpeed, float friction)
	{
		return (maxSpeed / friction) - maxSpeed;
	}
}