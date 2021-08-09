import numpy as np
from stable_baselines3 import DDPG
import os
import matplotlib.pyplot as plt
from stable_baselines3.common.noise import NormalActionNoise, OrnsteinUhlenbeckActionNoise
from stacker_env import StackerEnv
from stable_baselines3.common.monitor import Monitor
import pandas as pd
import argparse

def main():
    parser = argparse.ArgumentParser(description='Stacker parameters. (Example usage: "python ddpg.py -b stacker -l cube_stacking_model -t 500 --train" OR "python ddpg.py -b stacker -t 50 --test")')
    parser.add_argument('--log_dir', '-l', metavar='LOGDIR', default='.', help='log directory')
    parser.add_argument('--tb_name', '-b', metavar='TBNAME', default='.', help='TensorBoard path name')
    parser.add_argument('--total_timesteps', '-t', metavar='TOTALTIMESTEPS', default=500, help='total timesteps')
    parser.add_argument('--model_name', '-m', metavar='MODELNAME', default='ddpg_saved', help='name of model to save/load')
    parser.add_argument('--visual_obs', action='store_true', default=False, help='use visual observations')
    parser.add_argument('--vector_obs', action='store_true', default=False, help='use vector observations')
    parser.add_argument('--train', action='store_true', default=False, help='train mode')
    parser.add_argument('--test', action='store_true', default=False, help='test mode')

    args = parser.parse_args()

    log_dir = args.log_dir
    tb_name = args.tb_name
    total_timesteps = int(args.total_timesteps)
    model_name = args.model_name
    visual_obs = args.visual_obs
    vector_obs = args.vector_obs
    train = args.train
    test = args.test
    
    if not visual_obs and not vector_obs:
        visual_obs = True
        vector_obs = True
    
    os.makedirs(log_dir, exist_ok=True)

    env = StackerEnv(visual_observation=visual_obs,vector_observation=vector_obs)
    env = Monitor(env, log_dir)

    print("observation_space:", env.observation_space.shape)
    print("action_space_high:", env.action_space.high)
    print("action_space_low:", env.action_space.low)

    n_actions = env.action_space.shape[-1]
    print("n_actions", n_actions)

    action_noise = NormalActionNoise(mean=np.zeros(n_actions), sigma=0.1 * np.ones(n_actions))

    if train:
        if visual_obs and vector_obs:
            model = DDPG("MultiInputPolicy", env, learning_rate=1e-4, action_noise=action_noise, verbose=1, tensorboard_log="./" + tb_name + "/")
        elif visual_obs:
            model = DDPG("CnnPolicy", env, learning_rate=1e-4, action_noise=action_noise, verbose=1, tensorboard_log="./" + tb_name + "/")
        elif vector_obs:
            model = DDPG("MlpPolicy", env, learning_rate=1e-4, action_noise=action_noise, verbose=1, tensorboard_log="./" + tb_name + "/", learning_starts=0)

        model.learn(total_timesteps=total_timesteps)

        print("Done learning")

        model.save(log_dir + "/" + model_name)

        print("Model saved at", log_dir + "/" + model_name)

    if test:
        model = DDPG.load(log_dir + "/" + model_name)
        print(model)
        print(model.policy)
        print(model.get_parameters())

        obs = env.reset()

        i = 0
        while True:
            print("Testing %s" % i)
            print("obs:", obs)
            action, _states = model.predict(obs)
            print("action:", action)
            last_obs = obs
            obs, rewards, dones, info = env.step(action)
            print("obs:", obs)
            print(dones)
            if dones:
                env.reset()
                
            if visual_obs and vector_obs:
                if not np.allclose(obs["visual_obs"], last_obs["visual_obs"]) and\
                 not np.allclose(obs["vector_obs"], last_obs["vector_obs"]):
                    i += 1
            elif visual_obs:
                if np.allclose(obs, last_obs):
                    i += 1
            elif vector_obs:
                if np.allclose(obs, last_obs):
                    i += 1
                
            if i == total_timesteps:
                break

if __name__ == "__main__":
    main()
