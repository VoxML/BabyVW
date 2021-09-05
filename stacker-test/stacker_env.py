import numpy as np
import gym
from gym import spaces

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.base_env import ActionTuple

class StackerEnv(gym.Env):
    """
    Description:
        A number of objects are placed in an otherwise empty environment. All
        objects start on the floor and the goal is to place all objects into
        a single stack by placing each in turn  on top of the current stack so
        that the stack stays upright after the placement is complete.
        
    Observation:
        Type: Discrete (4 scalar values)
        The size of this space should be adjusted to always be greater than the
        total number of potentially-stackable objects in the environment. The
        number represents the height of the highest object tower constructed so
        far, i.e., 1 is the default observation because all objects are alone on
        the ground.
        Num     Observation
        0       0 objects have been successfully stacked (would only occur if the environment were empty)
        1       1 object has been successfully stacked (default state because all objects are on the ground)
        2       2 objects have been successfully stacked (highest structure in the environment is a 2-block stack)
        3       3 objects have been successfully stacked
        
    Actions:
        Type: Box (2 dimensions)
        The action space represents where, as a ratio in two dimensions, the theme
        object should be placed relative to the top surface of the destination
        object. That is, an action of [0.0,0.0] would place the theme object at
        the exact center of the top of the destination object, while an action of
        [-1.0,0.0] would place it at the far left edge. The actions, when received
        by Unity, are scaled to the size of the destination object and transformed
        into 3D space, so only these two values need to be provided by the RL client.
        Num     Action                          Min                     Max
        0       X value of object placement     -1.0                    1.0
        1       Y value of object placement     -1.0                    1.0
        
    Reward:
        Reward is 1 if an action results in a stack that is one object higher and
        stable. Reward is -1 if an action results in one object falling off the stack,
        -2 if it results in two objects falling off the stack, etc.
                
    Starting State:
        The starting observation is 1, because all objects are on the ground, therefore
        the "highest stack" is 1 block high.
                
    Episode Termination:
        All objects in the environment have been successfully stacked.
        Agent has tried and failed a number of times equivalent to the
        "Max Trials" value specified in the "StackingAgent" class in the
        Unity Editor.
        Episode length is greater than the "Max Step" value specified in the
        "StackingAgent" class in the Unity Editor.
        Solved Requirements:
        Because the reward is contructed in terms of "did the theme object stay where
        it was placed?" vs. "did the theme object fall down?" a successful policy
        should learn to produce action values that are closer to [0.0,0.0] in this
        action space. Considered solved when the average return is greater than or
        equal to 0.75 over 100 episodes, meaning a 3/4 probability of placing the theme
        object in a stable position on the first try, with no more than one failed
        try per episode.
    """

    def __init__(self,
        environment_filename=None,
        visual_observation=False,
        vector_observation=False):
        self._env = UnityEnvironment(environment_filename,0)
        self.seed()
        self.resetting = False
        self.num_timesteps = 0
        
        self.visual_obs = visual_observation
        self.vector_obs = vector_observation
        self.dict_obs = self.visual_obs and self.vector_obs
        
        self.raw_image_space = spaces.Box(
            0,
            255,
            dtype=np.uint8,
            shape=(84, 84, 3)
        )
        
        self.normalized_image_space = spaces.Box(
            0.0,
            1.0,
            dtype=np.float32,
            shape=(84, 84, 3)
        )
        
        self.vector_obs_space = spaces.Box(
            0.0,
            4.0,
            dtype=np.float32,
            shape=(1,)
        )
        
        if self.dict_obs:
            self.image_space = self.normalized_image_space
        elif self.visual_obs:
            self.image_space = self.raw_image_space
            
        self.reset()
        
        # get behavior name from Unity
        self.behavior_name = list(self._env.behavior_specs)[0]
        print(self.behavior_name)

        # get behavior spec
        self.behavior_spec = self._env.behavior_specs[self.behavior_name]
        print(self.behavior_spec)

        # define action and observation space
        # action: where on the surface of the target block do I put my object?
        # observation: how tall is the tallest thing in the world?
        self.action_space = spaces.Box(np.array([-1,-1]),
            np.array([1,1]))
                    
        if self.dict_obs:
            self.observation_space = spaces.Dict(
                spaces={
                    "visual_obs" : self.image_space,
                    "vector_obs" : self.vector_obs_space
                })
        elif self.visual_obs:
            self.observation_space = self.image_space
        elif self.vector_obs:
            self.observation_space = self.vector_obs_space

        self.last_action = np.array([-float('inf'),-float('inf')])
        self.last_obs = {}

    def step(self, action):
        self.num_timesteps += 1
        if self.resetting:
            self.resetting = False
        
        if not np.allclose(action,self.last_action):
            print("last_action:",self.last_action)
            print("action:",action)
        self._env.set_actions(self.behavior_name, ActionTuple(continuous=action.reshape((1,-1))))
        print("action shape:", action.reshape((1, -1)).shape)
        self._env.step()
        step_info, terminal_info = self._env.get_steps(self.behavior_name)

        obs = 1

        done = (len(terminal_info) != 0)
        
        if done:
            print("Terminated\n\tObservation: %s\tReward: %s\tInterrupted: %s" % (terminal_info.obs,terminal_info.reward,terminal_info.interrupted))
            
            if self.dict_obs:
                obs["visual_obs"] = terminal_info.obs[0]
                obs["vector_obs"] = terminal_info.obs[1]
            else:
                obs = terminal_info.obs[0]
            reward = terminal_info.reward[0]
            self.last_action = np.array([-float('inf'),-float('inf')])
        else:
            self.last_obs = obs
            self.last_action = action
        
            if self.dict_obs:
                obs = {}
                            
                if step_info.obs[0].shape[0] > 0:
                    obs["visual_obs"] = step_info.obs[0]
                else:
                    print("step_info.obs[0].shape = ", step_info.obs[0].shape, "setting visual_obs to black")
                    obs["visual_obs"] = np.zeros(self.image_space.shape, dtype=self.image_space.dtype)
                    
                if step_info.obs[1].shape[0] > 0:
                    obs["vector_obs"] = step_info.obs[1]
                else:
                    print("step_info.obs[1].shape = ", step_info.obs[1].shape, "setting vector_obs to 0")
                    obs["vector_obs"] = 0
            else:
                if step_info.obs[0].shape[0] > 0:
                    obs = step_info.obs[0]
                    
                    if self.visual_obs:
                        obs = (obs*255).astype('float32')
                else:
                    if self.visual_obs:
                        print("step_info.obs[0].shape =", step_info.obs[0].shape[0], "setting visual_obs to black")
                        obs = np.zeros(self.image_space.shape, dtype=self.image_space.dtype)
                    elif self.vector_obs:
                        print("step_info.obs[0].shape =", step_info.obs[0].shape[0], "setting vector_obs to 0")
                        obs = 0

            print("Step\n\tObservation: %s\tReward: %s" % (step_info.obs,step_info.reward))
            if not np.allclose(action,self.last_action):
                print("last observation:",self.last_obs)

            if step_info.reward.shape[0] > 0:
                reward = step_info.reward[0]
            else:
                print("step_info.reward.shape =", step_info.reward.shape, "setting reward to 0")
                reward = 0
                
            if not np.allclose(action,self.last_action):
                print("reward:",reward)

        info = {}
        return obs, reward, done, info

    def reset(self):
        if self.resetting:
            return
        print("Resetting")
        #if self.num_timesteps > 10
        #    assert False
        self.resetting = True
        self.seed()
        obs = self._env.reset()
        if obs is None:
            if self.dict_obs:
                obs = {}
                obs["visual_obs"] = np.zeros(self.image_space.shape, dtype=self.image_space.dtype)
                obs["vector_obs"] = np.array([1+np.random.normal(0,0.1,1)[0]]) # add gaussian noise
            elif self.visual_obs:
                obs = np.zeros(self.image_space.shape, dtype=self.image_space.dtype)
            elif self.vector_obs:
                obs = np.array([1+np.random.normal(0,0.1,1)[0]]) # add gaussian noise
            print("obs is None, setting obs to", obs)
        self.resetting = False
        return obs

    #def render(self, mode='rgb_array'):
        #return self.visual_obs

    def close(self):
        self._env.close()
        
    def seed(self, seed=None):
        """Sets a fixed seed for this env's random number generator(s).
        The valid range for seeds is [0, 99999). By default a random seed
        will be chosen.
        """
        if seed is None:
            self._seed = seed
            return

        seed = int(seed)
        if seed < 0 or seed >= 99999:
            print("Seed outside of valid range [0, 99999). A random seed within the valid range will be used on next reset.")
        print("New seed " + str(seed) + " will apply on next reset.")
        self._seed = seed
