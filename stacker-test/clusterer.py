import argparse
import itertools
import matplotlib.pyplot as plt
import numpy as np
import neuralnetworks_torch as nntorch
import os
import pandas
from scipy.optimize import linear_sum_assignment as linear_assignment
from sklearn import metrics
from sklearn.cluster import DBSCAN
from sklearn.cluster import KMeans
from sklearn.metrics import silhouette_score
from sklearn.metrics import confusion_matrix, classification_report
from sklearn.preprocessing import StandardScaler
import torch

idx_to_labels = {
    -1 : "outlier",
    0 : "cube",
    1 : "sphere",
    2 : "cylinder"
}

def main():
    parser = argparse.ArgumentParser(description='Autoencoder and clustering.  (Example usage: "python clusterer.py -f trial-data/3_obj_dataC.csv --ep_final -m autoencoders/3_obj_modelC.pt")')
    parser.add_argument('--file', '-f', metavar='FILE', default='.', help='source data file')
    parser.add_argument('--model', '-m', metavar='MODEL', default=None, help='model file name (saves model if --retrain is true, otherwise tries to load a model from this file)')
    parser.add_argument('--retrain', '-r', action='store_true', default=False, help='retrain?')
    parser.add_argument('--num_means', '-k', metavar='K', default=None, help='specify number of means (default None = use DBSCAN, 0 = automatically estimate k and use k-means)')
    parser.add_argument('--ep_final', action='store_true', default=False, help='use episode final state only')
    parser.add_argument('--elbow', action='store_true', default=False, help='use elbow k calculation')
    parser.add_argument('--silhouette', action='store_true', default=False, help='use silhouette k calculation')
    parser.add_argument('--mean', action='store_true', default=False, help='use mean of elbow and silhouette k calculation')

    args = parser.parse_args()

    filepath = args.file
    modelpath = args.model
    retrain = args.retrain
    specified_k = int(args.num_means) if args.num_means is not None else None
    ep_final = args.ep_final
    elbow_k_calc = args.elbow
    silhouette_k_calc = args.silhouette
    mean_k_calc = args.mean
    
    if not elbow_k_calc and not silhouette_k_calc and not mean_k_calc:
        elbow_k_calc = True
        
    np.set_printoptions(precision=7, suppress=True)

    df = pandas.read_csv(filepath, header=None)
    print(df.shape)
    print(df)
    
    #df = df.loc[df[1] < 2]
    
    if ep_final:
        episode_terminal_data = []
        
        for i in df[1].unique():
            class_data = df.loc[df[1] == i]
            for j in class_data[0].unique():
                episode_terminal_data.append(df.loc[class_data.where(class_data == j).last_valid_index()])
            
        df = pandas.DataFrame(episode_terminal_data).to_numpy()
    else:
        df = df.values
        
    print(pandas.DataFrame(df))
    input("Press Return to continue")

    #df = np.array([v for v in df if v[8] < 1.5])
        
    # columns:
    #   0: episode #
    #   1: theme obj
    #   2: dest obj
    #   3-5: theme obj rotation
    #   6-7: action
    #   8: observation
    #   9: reward
    #   10: ep. total reward
    #   11: ep. mean reward

    X = np.hstack([df[:, 3:10]])
    T = df[:, 1:2]
    print(X.shape, T.shape)
    
    n_in = X.shape[1]
    n_out = n_in
    
    hiddens = [256, 16, 4, 2, 16, 256, 8192]
    act_funcs = ['tanh','tanh','tanh','silu','prelu','prelu','prelu']
        
    bottleneck = np.array([])
    
    if retrain:
        if modelpath is not None:
            if os.path.exists(modelpath):
                os.remove(modelpath)
            
        nnet = nntorch.NeuralNetwork(n_in, hiddens, n_out, act_func_per_layer=act_funcs)
        
        nnet.fit(X, X, 5000, 0.001, method='adam', verbose=True)
        plt.plot(nnet.error_trace)
        plt.show()
        
        bottleneck = nnet.use_to_middle(X)
        print(bottleneck.shape)
        
        if modelpath is not None:
            torch.save(nnet, modelpath)
    elif modelpath is not None:
        nnet = torch.load(modelpath)
        nnet.eval()
        
        print(nnet)
    
        bottleneck = nnet.use_to_middle(X)
        print(bottleneck.shape)
            
    idxs = np.argsort(np.hstack([np.array(range(X.shape[0])).reshape(-1,1),bottleneck])[:, 1])
    
    for i in idxs:
        print("%s\t%s" % (i, bottleneck[i]))
    
    plt.figure(figsize=(12, 10))
    plt.scatter(bottleneck[:, 0], bottleneck[:, 1], c=T.flat, alpha=0.5, marker="X")
    plt.colorbar();
    plt.show()
    
    if specified_k == None:
        scaled_bottleneck = StandardScaler().fit_transform(bottleneck)
    
        # can we automatically calculate the best epsilon?
        db = DBSCAN(eps=.4, min_samples=7).fit(scaled_bottleneck)
        core_samples_mask = np.zeros_like(db.labels_, dtype=bool)
        core_samples_mask[db.core_sample_indices_] = True
        
        predictions = db.labels_
        labels = T.reshape(-1,).astype(int)

        # Number of clusters in labels, ignoring noise if present.
        k = len(set(predictions)) - (1 if -1 in predictions else 0)
        n_noise_ = list(predictions).count(-1)

        print("Estimated number of clusters: %d" % k)
        print("Estimated number of noise points: %d" % n_noise_)
        print("Homogeneity: %0.3f" % metrics.homogeneity_score(labels, predictions))
        print("Completeness: %0.3f" % metrics.completeness_score(labels, predictions))
        print("V-measure: %0.3f" % metrics.v_measure_score(labels, predictions))
        print("Adjusted Rand Index: %0.3f"
              % metrics.adjusted_rand_score(labels, predictions))
        print("Adjusted Mutual Information: %0.3f"
              % metrics.adjusted_mutual_info_score(labels, predictions))
        print("Silhouette Coefficient: %0.3f"
              % metrics.silhouette_score(scaled_bottleneck, predictions))
    else:
        if specified_k == 0:
            wss_scores = elbow(bottleneck, 10)
            print(wss_scores)
            print(np.gradient(wss_scores))
            elbow_k = np.argmin(np.gradient(wss_scores))+2
            
            sil_scores = silhouette(bottleneck, 10)
            print(sil_scores)
            sil_k = np.argmax(sil_scores)+2
            
            plt.figure(figsize=(12, 5))
            plt.subplot(1,2,1)
            plt.plot(range(1,11),wss_scores)
            plt.subplot(1,2,2)
            plt.plot(range(2,11),sil_scores)
            plt.show()

            print("Elbow k:", elbow_k)
            print("Silhouette k:", sil_k)
            print("Mean k:", np.mean([elbow_k,sil_k]), "->", int(np.mean([elbow_k,sil_k])))
            
            k = elbow_k
            
            if silhouette_k_calc:
                k = sil_k
            elif mean_k_calc:
                k = int(np.mean([elbow_k,sil_k]))
        else:
            k = specified_k
            
        kmeans = KMeans(n_clusters=k, random_state=0).fit(bottleneck)
        print(kmeans.cluster_centers_)
        
        predictions = kmeans.labels_
        labels = T.reshape(-1,).astype(int)
        
    print("Average clustering accuracy = %.4f%%" % (100 * cluster_acc(labels, predictions)))
            
    plt.figure(figsize=(12, 5))
    plt.subplot(1,2,1)
    plt.scatter(bottleneck[:, 0], bottleneck[:, 1], c=T.flat, alpha=0.5, marker="X")
    plt.colorbar();
    plt.subplot(1,2,2)
    
    if specified_k == None:
        unique_labels = set(predictions)
        colors = [plt.cm.Spectral(each)
                  for each in np.linspace(0, 1, len(unique_labels))]
        for i, col in zip(unique_labels, colors):
            if i == -1:
                # Black used for noise.
                col = [0, 0, 0, 1]

            ind, w = align_indices(labels,predictions)
            aligned_pred = align_predictions(predictions,ind)
            class_member_mask = (aligned_pred == i)

            xy = bottleneck[class_member_mask & core_samples_mask]
            plt.plot(xy[:, 0], xy[:, 1], 'o', markerfacecolor=tuple(col),
                     markeredgecolor='k', markersize=12, label=i, alpha=0.5)

            xy = bottleneck[class_member_mask & ~core_samples_mask]
            plt.plot(xy[:, 0], xy[:, 1], 'o', markerfacecolor=tuple(col),
                     markeredgecolor='k', markersize=6, label=i, alpha=0.5)
    else:
        classes = []
    
        for i in range(k):
            classes.append(bottleneck[predictions == i])
        for i in range(k):
            plt.scatter(classes[i][:, 0], classes[i][:, 1], label=i, alpha=0.5)
        plt.scatter(kmeans.cluster_centers_[:, 0], kmeans.cluster_centers_[:, 1], color='b', marker='s', label='Centroid')
    plt.legend()
    plt.show()
    
    cmat = confusion_matrix(labels, align_predictions(predictions,align_indices(labels,predictions)[0]),labels=list(set(predictions)))
    
    print(classification_report(labels, align_predictions(predictions,align_indices(labels,predictions)[0]), target_names=[idx_to_labels[l] for l in sorted(set(predictions))],
        zero_division=0))
    
    fig = plt.figure()
    ax = fig.add_subplot(111)
    cax = ax.matshow(cmat)
    fig.colorbar(cax)
    ax.set_xticklabels([''] + [idx_to_labels[l] for l in list(set(predictions))])
    ax.set_yticklabels([''] + [idx_to_labels[l] for l in list(set(predictions))])
    for i, j in itertools.product(range(cmat.shape[0]), range(cmat.shape[1])):
        plt.text(j, i, "{:,}".format(cmat[i, j]),
                     horizontalalignment="center",
                     color="black" if cmat[i, j] > cmat.max()/2 else "white")
    plt.xlabel('Predicted')
    plt.ylabel('Actual')
    plt.show()
    
    inspect(bottleneck, X, nnet)
    
def elbow(data, kmax):
    sse = []
    for k in range(1, kmax+1):
        kmeans = KMeans(n_clusters=k).fit(data)
        centroids = kmeans.cluster_centers_
        pred_clusters = kmeans.predict(data)
        curr_sse = 0
    
        # calculate square of Euclidean distance of each point from its cluster center and add to current WSS
        for i in range(len(data)):
            curr_center = centroids[pred_clusters[i]]
            curr_sse += (data[i, 0] - curr_center[0]) ** 2 + (data[i, 1] - curr_center[1]) ** 2
      
        sse.append(curr_sse)
    return sse
    
def silhouette(data, kmax):
    sil = []

    # dissimilarity would not be defined for a single cluster, thus, minimum number of clusters should be 2
    for k in range(2, kmax+1):
        kmeans = KMeans(n_clusters = k).fit(data)
        labels = kmeans.labels_
        sil.append(silhouette_score(data, labels, metric='euclidean'))
        
    return sil
    
def cluster_acc(y_true, y_pred):
    """
    Calculate clustering accuracy. Require scikit-learn installed
    # Arguments
        y: true labels, numpy.array with shape `(n_samples,)`
        y_pred: predicted labels, numpy.array with shape `(n_samples,)`
    # Return
        accuracy, in [0,1]
    """
    ind, w = align_indices(y_true,y_pred)
    print("Predicted:", align_predictions(y_pred,ind))
    print("Actual:", y_true)
    
    return sum([w[i, j] for i, j in ind]) * 1.0 / y_pred.size
    
def align_indices(y_true, y_pred):
    y_true = y_true.astype(np.int64)
    assert y_pred.size == y_true.size
    D = max(y_pred.max(), y_true.max()) + 1
    w = np.zeros((D, D), dtype=np.int64)
    for i in range(y_pred.size):
        w[y_pred[i], y_true[i]] += 1
    ind = linear_assignment(w.max() - w)
    ind = np.asarray(ind)
    ind = np.transpose(ind)
    return ind, w

def align_predictions(predictions,ind):
    return np.array([[pair for pair in zip(ind[:, 0],ind[:, 1])][y][1] if y != -1 else -1 for y in predictions])
    
def inspect(data, df, nnet):
    while(True):
        try:
            point_index = int(input("Inspect bottlenecked point # (leave blank to quit) "))
        except ValueError:
            exit()
        
        if point_index < data.shape[0]:
            print("X\t\t", df[point_index])
            print("bottleneck\t", data[point_index])
            print("X'\t\t", nnet.use(df[point_index]))
        else:
            print("Invalid selection")

if __name__ == "__main__":
    main()
