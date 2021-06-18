import numpy as np
from stable_baselines3 import TD3
from stable_baselines3 import DDPG
from stable_baselines3.common.noise import NormalActionNoise, OrnsteinUhlenbeckActionNoise
from stacker_env import StackerEnv
from test_policy import TestPolicy



env = StackerEnv()


print("observation_space:", env.observation_space.shape)
print("action_space_high:", env.action_space.high)
print("action_space_low:", env.action_space.low)

n_actions = env.action_space.shape[-1]
print("n_actions", n_actions)

action_noise = NormalActionNoise(mean=np.zeros(n_actions), sigma=0.1 * np.ones(n_actions))


model = DDPG("MlpPolicy", env, learning_rate=1e-4, action_noise=action_noise, verbose=1, tensorboard_log="./ddpg_tensorboard/")


#model = TD3(TestPolicy, env, target_policy_noise = 0.002, learning_rate=1e-4, action_noise=action_noise, verbose=1, tensorboard_log="./ddpg_tensorboard/")
model.learn(total_timesteps=10000)

model.save("ddpg_test")
env = model.get_env()

del model

model = DDPG.load("ddpg_test")

obs = env.reset()
while True:
    action, _states = model.predict(obs)
    obs, rewards, dones, info = env.step(action)
    #env.render()
















