# Multiplayer Vehicle Physics | Tech Demo
This repository combines vehicle simulation and networking in a Godot project. The project is created to illustrate the skills of the author within the subjects:

* System architecture
* Physically-based systems
    * Numerical vehicle simulation
* High-level peer-to-peer networking
* ...

A simple game loop is implemented to allow the local and remote peers to play together.

***==================***<br/>
***insert example image/GIF here!***<br/>
***==================***

## Structure and topics of the code

***insert UML here!***

### Tire dynamics
The forces produced by a tire are unlike those exhibited by other joint types. In the real world, because the rubber of the tire gets displaced and is attached to a two-DoF axle (yaw and pitch), the forces take on non-linear properties that are highly dependent on the characteristics of the contact patch between the tire and the surface. As such, a specialized joint ([WheelJoint.cs](Scripts\Joints\WheelJoint.cs)) is implemented which models this behaviour.

It is common for tire simulations to distinguish between the local axes, XYZ, of the tire and the forces they produce. The vertical axis, Y in this case, generally doesn't produce any meaningful forces. The other two which, for most cases, align with the contact surface are referred to as: lateral (X), and longitudinal (Z). The lateral force is what corrects the trajectory of the vehicle (i.e. steers the vehicle) while the longitudinal force affects its speed.

In the abstract, the magnitude of these forces is formulated as:
> `||F|| = wheel_load * slip`

Slip is differentiated for both the lateral (s_x) and longitudinal (s_z) axes while wheel load is a single variable. The equation used for the slip values can be found in the aforementioned script. Note that edge cases are resolved in situations where the slip value becomes unstable due to numerical precision limits (e.g. when the denominator of a division approaches `0.0`).

<details>
    <summary>Sidenote</summary>
    
*There are other factors that affect the slip-value or the force vector (such as camber) which are not described here. Please inspect the code if you're interested.*

*It should also be noted that the implementation of the tire joint, as well as this surface-level description of it, only attempts to approximate realistic behaviour that is suitable for gameplay purposes.*
</details>

### Spring dynamics
Apart from the tire and its forces, the [WheelJoint.cs](Scripts\Joints\WheelJoint.cs) class also implements the simulation for the spring between the tire and the vehicle body.

The force of the spring is computed using Hooke's Law.

### Networking
The networking seen in this demo utilizes the high-level networking framework in Godot (See [docs](https://docs.godotengine.org/en/stable/tutorials/networking/high_level_multiplayer.html)). The framework is based on [ENet](http://enet.bespin.org/index.html) and uses Remote Procedure Calls (RPC) to communicate between peers.

In order to create a lobby that can be connected to by remote clients, the host must port forward the demo's port number on their router.

The "netcode" for this project is client-side meaning that events and states that occur/exist on the local client is shared with the remote peers &ndash; it is **not** server-authoritative. Events, such as health modification or ability activations, are replicated through a *reliable* RPC while state synchronization is replicated through an *unreliable* RPC that ignores out-of-order packets (See [docs](https://docs.godotengine.org/en/stable/tutorials/networking/high_level_multiplayer.html#remote-procedure-calls)).


## Other

### Engine class