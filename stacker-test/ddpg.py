import numpy as np
#from stable_baselines import DDPG, A2C, PPO1
from stable_baselines3 import DDPG
import os
import matplotlib.pyplot as plt
#from stable_baselines3.common.noise import NormalActionNoise, OrnsteinUhlenbeckActionNoise
#from stable_baselines.common.noise import NormalActionNoise
from stacker_env import StackerEnv
#from stable_baselines3.common.monitor import Monitor
import pandas as pd
import argparse, textwrap
#from stable_baselines.common import make_vec_env
#from stable_baselines3.common.env_util import make_vec_env



def main():
    parser = argparse.ArgumentParser(description=textwrap.dedent('''
        Stacker parameters.
        Example usage:
        \tTrain new model for 500 timesteps using vector observations only, and save the model in the "cube_stacking_model" directory: "python ddpg.py -b stacker -l cube_stacking_model -t 500 --train --vector_obs"
        \tTest ddpg_1 for 50 timesteps: "python ddpg.py -b stacker -m ddpg_1 -t 50 --test"
        \tLoad ddpg_1, continue training for 100 timesteps, and save the result as "ddpg_2": "python ddpg.py -b stacker -m ddpg_1 -M ddpg_2" -t 100 --train"
        '''),formatter_class=argparse.RawTextHelpFormatter)
    parser.add_argument('--log_dir', '-l', metavar='LOGDIR', default='.', help='log directory')
    parser.add_argument('--tb_name', '-b', metavar='TBNAME', default='.', help='TensorBoard path name')
    parser.add_argument('--total_timesteps', '-t', metavar='TOTALTIMESTEPS', default=500, help='total timesteps')
    parser.add_argument('--model_name', '-m', metavar='MODELNAME', default='ddpg_saved', help='name of model to save/load')
    parser.add_argument('--new_model_name', '-M', metavar='NEWMODELNAME', default=None, help='name of new model to save if fine-tuning starting with another model')
    parser.add_argument('--visual_obs', action='store_true', default=False, help='use visual observations (leave blank to use both)')
    parser.add_argument('--vector_obs', action='store_true', default=False, help='use vector observations (leave blank to use both)')
    parser.add_argument('--priors', '-p', metavar='PRIORS', type=str, nargs='+', help='set of priors to use (one required): HGT = height; REL = relations; COG = center of gravity')
    parser.add_argument('--train', action='store_true', default=False, help='train mode')
    parser.add_argument('--test', action='store_true', default=False, help='test mode')

    args = parser.parse_args()

    log_dir = args.log_dir
    tb_name = args.tb_name
    total_timesteps = int(args.total_timesteps)
    model_name = args.model_name
    new_model_name = args.new_model_name
    priors = sorted(args.priors)
    visual_obs = args.visual_obs
    vector_obs = args.vector_obs
    train = args.train
    test = args.test
    
    if not visual_obs and not vector_obs:
        visual_obs = True
        vector_obs = True
    
    os.makedirs(log_dir, exist_ok=True)

    env = StackerEnv(visual_observation=visual_obs,vector_observation=vector_obs,priors=priors)

    #env = make_vec_env(lambda:env, n_envs=2)

    #env = Monitor(env, log_dir)

    print("observation_space:", env.observation_space.shape)
    print("action_space_high:", env.action_space.high)
    print("action_space_low:", env.action_space.low)

    n_actions = env.action_space.shape[-1]
    print("n_actions", n_actions)

    action_noise = NormalActionNoise(mean=np.zeros(n_actions), sigma=0.1 * np.ones(n_actions))

    if train:
        if new_model_name is None:
            if visual_obs and vector_obs:
                model = DDPG("MultiInputPolicy", env, learning_rate=1e-4, action_noise=action_noise, verbose=1, tensorboard_log="./" + tb_name + "/")
            elif visual_obs:
                model = DDPG("CnnPolicy", env, learning_rate=1e-4, action_noise=action_noise, verbose=1, tensorboard_log="./" + tb_name + "/")
            elif vector_obs:
                model = DDPG("MlpPolicy", env, learning_rate=1e-4, action_noise=action_noise, verbose=1, tensorboard_log="./" + tb_name + "/")
        else:
            model = DDPG.load(log_dir + "/" + model_name, env)

        model.learn(total_timesteps=total_timesteps)

        print("Done learning")

        if new_model_name is None:
            model.save(log_dir + "/" + model_name)
            print("Model saved at", log_dir + "/" + model_name)
        else:
            model.save(log_dir + "/" + new_model_name)
            print("Model saved at", log_dir + "/" + new_model_name)

    if test:
        model = DDPG.load(log_dir + "/" + model_name)
        print(model)
        print(model.policy)
        print(model.get_parameters())

        obs = env.reset()

        i = 0
        ep_reward = 0
        total_reward = 0
        max_reward = 0
        total_episodes = 0
        while True:
            print("\nTesting %s" % i)
            print("obs:", obs)
            action, _states = model.predict(obs)
            last_obs = obs
            obs, reward, done, info = env.step(action)
            print("Reward: %s\t Done: %s" % (reward,done))
            total_reward += reward
            ep_reward += reward
            if done:
                max_reward = ep_reward if ep_reward > max_reward else max_reward
                total_episodes += 1
                ep_reward = 0
                env.reset()
                
            if visual_obs and vector_obs:
                if not np.allclose(obs["visual_obs"], last_obs["visual_obs"]) and\
                 not np.allclose(obs["vector_obs"], last_obs["vector_obs"]):
                    i += 1
            elif visual_obs:
                if not np.allclose(obs, last_obs):
                    i += 1
            elif vector_obs:
                if not np.allclose(obs, last_obs):
                    i += 1
                
            if i == total_timesteps:
                break
                
        print("\n===== Summary =====")
        print("Tested for %s timesteps" % i)
        print("\t%s episodes" % total_episodes)
        print("\tTotal reward: %s" % total_reward)
        print("\tMax reward achieved: %s" % max_reward)
        print("\tMean reward per episode: %.4f" % (float(total_reward)/float(total_episodes),))

if __name__ == "__main__":
    main()
