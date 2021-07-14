import os
from stacker_env import StackerEnv
from stable_baselines3.common.monitor import Monitor
from stable_baselines3 import SAC



log_dir = "/Users/sadaf/PycharmProjects/tmp/"
os.makedirs(log_dir, exist_ok=True)


env = StackerEnv()
env = Monitor(env, log_dir)


print("observation_space:", env.observation_space.shape)
print("action_space_high:", env.action_space.high)
print("action_space_low:", env.action_space.low)

n_actions = env.action_space.shape[-1]
print("n_actions", n_actions)

model = SAC("MlpPolicy",  env, learning_rate=0.00001, verbose=1)
model.learn(total_timesteps=10000)
model.save("sac_trial")


model = SAC.load("sac_trial")

obs = env.reset()
while True:
    print("Testing")
    action, _states = model.predict(obs, deterministic=True)
    obs, reward, done, info = env.step(action)
    if done:
      obs = env.reset()
