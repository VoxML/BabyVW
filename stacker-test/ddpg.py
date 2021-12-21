import numpy as np
from stable_baselines3 import DDPG
import os
import matplotlib.pyplot as plt
from stable_baselines3.common.noise import NormalActionNoise, OrnsteinUhlenbeckActionNoise
from stacker_env import StackerEnv
from stable_baselines3.common.monitor import Monitor
import pandas as pd
import argparse, textwrap
from stable_baselines3.common.env_util import make_vec_env
import time
from datetime import datetime

def main():
    parser = argparse.ArgumentParser(description=textwrap.dedent('''
        Stacker parameters.
        Example usage:
        \tTrain new model for 500 timesteps using vector observations only with height and center of gravity priors, and save the model (using default naming conventions) in the "cube_stacking_model" directory: "python ddpg.py -b stacker -l cube_stacking_model -t 500 --train --vector_obs -p HGT COG"
        \tTest ddpg_1 for 50 timesteps using height prior only: "python ddpg.py -b stacker -m ddpg_1 -t 50 --test -p HGT"
        \tLoad ddpg_1, continue training for 100 timesteps using center of gravity prior only, and save the result as "ddpg_2": "python ddpg.py -b stacker -m ddpg_1 -M ddpg_2 -t 100 --train -p COG"
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
                model = DDPG("MlpPolicy", env, learning_rate=1e-3, action_noise=action_noise, verbose=1, tensorboard_log="./" + tb_name + "/")
        else:
            model = DDPG.load(log_dir + "/" + model_name, env)
            
        print(model.policy)

        start_time = time.time()
        model.learn(total_timesteps=total_timesteps, log_interval=1)

        print("Done learning")
        print("Training took %.4f seconds" % float(time.time()-start_time))

        if new_model_name is None:
            # create filename: model name + date + action space + trainings steps + priors
            filename = model_name + "-" + datetime.now().strftime("%Y%m%d") + "-" + \
                str(",".join(list(map(str,env.action_space.low)))) + "-" + str(",".join(list(map(str,env.action_space.high)))) + \
                "-" + str(total_timesteps) + "-" + ".".join(priors)
            num_identical_filenames = len([f for f in os.listdir(filename.rsplit('/')[0]) if f.startswith(filename.rsplit('/')[-1])])
            if num_identical_filenames > 0:
                filename += "-" + str(num_identical_filenames+1)
            model.save(log_dir + "/" + filename)
            print("Model saved at", log_dir + "/" + filename)
        else:
            # create filename: model name + date + action space + trainings steps + priors
            filename = new_model_name + "-" + datetime.now().strftime("%Y%m%d") + "-" + \
                str(",".join(list(map(str,env.action_space.low)))) + "-" + str(",".join(list(map(str,env.action_space.high)))) + \
                "-" + str(total_timesteps) + "-" + ".".join(priors)
            num_identical_filenames = len([f for f in os.listdir(filename.rsplit('/')[0]) if f.startswith(filename.rsplit('/')[-1])])
            if num_identical_filenames > 0:
                filename += "-" + str(num_identical_filenames+1)
            model.save(log_dir + "/" + filename)
            print("Model saved at", log_dir + "/" + filename)

    if test:
        model = DDPG.load(log_dir + "/" + model_name)
        print("Loaded model", log_dir + "/" + model_name)
        #print(model)
        #print(model.policy)
        #print(model.get_parameters())
        
        # reset the environment and get the resulting observation
        env.reset()
        obs = env._env._env_state[env.behavior_name][0].obs[0]

        i = 1
        ep_reward = 0
        total_reward = 0
        max_reward = 0
        total_episodes = 0
        timesteps = []
        episodes = []
        reward_per_timestep = []
        reward_per_episode = []
        ts_reward_mean = []
        ep_reward_mean = []
        while True:
            print("\nTesting %s" % i)
            print("obs:", obs)
            action, _states = model.predict(obs)
            last_obs = obs
            obs, reward, done, info = env.step(action)
            print("Reward: %s\t Done: %s" % (reward,done))
            total_reward += reward
            ep_reward += reward
            timesteps.append(i)
            reward_per_timestep.append(reward)
            ts_reward_mean.append(float(total_reward)/float(i))
            if done:
                max_reward = ep_reward if ep_reward > max_reward else max_reward
                total_episodes += 1
                episodes.append(total_episodes)
                reward_per_episode.append(ep_reward)
                ep_reward_mean.append(float(total_reward)/float(total_episodes))
                ep_reward = 0
                # reset the environment and get the resulting observation
                env.reset()
                obs = env._env._env_state[env.behavior_name][0].obs[0]

#            if visual_obs and vector_obs:
#                if not np.allclose(obs["visual_obs"], last_obs["visual_obs"]) and\
#                 not np.allclose(obs["vector_obs"], last_obs["vector_obs"]):
#                    i += 1
#            elif visual_obs:
#                if not np.allclose(obs, last_obs):
#                    i += 1
#            elif vector_obs:
#                if not np.allclose(obs, last_obs):
                
            if i == total_timesteps:
                break
                
            i += 1

        print("\n===== Summary =====")
        print("Tested for %s timesteps" % i)
        print("\t%s episodes" % total_episodes)
        print("\tTotal reward: %s" % total_reward)
        print("\tMax reward achieved: %s" % max_reward)
        print("\tMean reward per episode: %.4f" % (float(total_reward)/float(total_episodes),))
        
        plt.figure(figsize=(16,6))
        plt.subplot(1, 2, 1)
        plt.plot(np.array(timesteps),np.array(reward_per_timestep), label="Raw Reward")
        plt.plot(np.array(timesteps),np.array(ts_reward_mean), label="Mean reward")
        plt.xlabel("Timesteps")
        plt.ylabel("Reward")
        plt.legend(loc="upper left")
        plt.ylim(-100, 1200.0)
        plt.gca().yaxis.grid()
        
        plt.subplot(1, 2, 2)
        plt.plot(np.array(episodes),np.array(reward_per_episode), label="Raw Reward")
        plt.plot(np.array(episodes),np.array(ep_reward_mean), label="Mean reward")
        plt.xlabel("Episodes")
        plt.ylabel("Reward")
        plt.legend(loc="upper left")
        plt.ylim(-100, 1200.0)
        plt.gca().yaxis.grid()
        plt.show()

if __name__ == "__main__":
    main()
