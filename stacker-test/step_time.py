from stacker_env import StackerEnv
import time
import numpy as np
env = StackerEnv(vector_observation=False, visual_observation= True)
obs = env.reset()
s = time.time()
for i in range(10000):
    obs, reward, done, info = env.step(np.array([0,0]))
    if done:
        env.reset()

    if (i + 1) % 10 == 0:
        print(f'%.3f sec/iter' % ((time.time() - s) / (i + 1)))

