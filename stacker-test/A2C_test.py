from stacker_env_disc import StackerEnv
#from stable_baselines.common.policies import MlpPolicy
#from stable_baselines.common import make_vec_env
from stable_baselines3 import A2C
from stable_baselines3.common.monitor import Monitor

env = StackerEnv()
env = Monitor(env, '.')

model = A2C("MlpPolicy", env, learning_rate=1e-4, verbose=1)
model.learn(total_timesteps=25000)
model.save("a2c_test")

del model

model = A2C.load("a2c_test")

obs = env.reset()
while True:
    action, _states = model.predict(obs)
    obs, rewards, dones, info = env.step(action)
